using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImprovementEngine.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ImprovementEngine.Services
{
    /// <summary>
    /// Handles model training, evaluation, staging, and promotion
    /// </summary>
    public class ModelTrainingService
    {
        private readonly ImprovementDbContext _dbContext;
        private readonly ILogger<ModelTrainingService> _logger;
        private readonly string _mlflowTrackingUri;
        private readonly string _modelRegistryPath;

        public ModelTrainingService(
            ImprovementDbContext dbContext,
            ILogger<ModelTrainingService> logger,
            string mlflowTrackingUri = "http://localhost:5000",
            string modelRegistryPath = "/models")
        {
            _dbContext = dbContext;
            _logger = logger;
            _mlflowTrackingUri = mlflowTrackingUri;
            _modelRegistryPath = modelRegistryPath;
        }

        /// <summary>
        /// Retrain a model with latest data
        /// </summary>
        public async Task<OptimizationResult> RetrainModelAsync(string modelType, CancellationToken ct)
        {
            _logger.LogInformation("Starting model retraining for {ModelType}", modelType);

            var newVersion = GenerateVersion();
            var startTime = DateTime.UtcNow;

            try
            {
                // 1. Extract training data
                var trainingData = await ExtractTrainingDataAsync(modelType, days: 90, ct);
                
                // 2. Feature engineering
                var features = await PrepareFeaturesAsync(trainingData, ct);
                
                // 3. Train model
                var trainingResult = await TrainModelAsync(modelType, features, newVersion, ct);
                
                // 4. Evaluate
                var evaluation = await EvaluateModelAsync(modelType, newVersion, trainingResult, ct);
                
                // 5. Store metrics
                await StoreModelPerformanceAsync(modelType, newVersion, evaluation, trainingData.Count, ct);
                
                // 6. Start A/B test if model is promising
                if (evaluation.SharpeRatio > 1.0 && evaluation.WinRate > 0.5)
                {
                    await StartABTestAsync(modelType, newVersion, ct);
                }

                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation("Model retraining completed in {Duration}s", duration.TotalSeconds);

                return new OptimizationResult
                {
                    ImprovementPercent = (decimal)(evaluation.ExpectedImprovement * 100),
                    Details = $"Trained model {newVersion} with accuracy {evaluation.Accuracy:P2}, Sharpe {evaluation.SharpeRatio:F2}",
                    Parameters = new Dictionary<string, object>
                    {
                        { "version", newVersion },
                        { "training_samples", trainingData.Count },
                        { "accuracy", evaluation.Accuracy },
                        { "sharpe_ratio", evaluation.SharpeRatio },
                        { "win_rate", evaluation.WinRate },
                        { "training_duration_sec", duration.TotalSeconds }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Model retraining failed for {ModelType}", modelType);
                throw;
            }
        }

        /// <summary>
        /// Promote a model to production
        /// </summary>
        public async Task PromoteModelAsync(string modelVersion, CancellationToken ct)
        {
            _logger.LogInformation("Promoting model {Version} to production", modelVersion);

            var modelPerf = await _dbContext.ModelPerformance
                .FirstOrDefaultAsync(m => m.ModelVersion == modelVersion, ct);

            if (modelPerf == null)
            {
                throw new InvalidOperationException($"Model {modelVersion} not found");
            }

            // Update status
            modelPerf.Status = "production";
            
            // Log the improvement event
            var improvementEvent = new ImprovementEvent
            {
                EventType = "model_deployed",
                ComponentType = "model",
                ComponentId = modelPerf.ModelType,
                NewValue = JsonSerializer.SerializeToDocument(new { version = modelVersion }),
                TriggerReason = "ab_test_winner",
                Automated = true
            };

            _dbContext.ImprovementEvents.Add(improvementEvent);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Model {Version} promoted to production", modelVersion);
        }

        /// <summary>
        /// Rollback to previous model version
        /// </summary>
        public async Task RollbackModelAsync(string modelType, string targetVersion, string reason, CancellationToken ct)
        {
            _logger.LogWarning("Rolling back {ModelType} to version {Version}. Reason: {Reason}", 
                modelType, targetVersion, reason);

            // Find current production model
            var currentModel = await _dbContext.ModelPerformance
                .Where(m => m.ModelType == modelType && m.Status == "production")
                .OrderByDescending(m => m.EvaluationDate)
                .FirstOrDefaultAsync(ct);

            if (currentModel != null)
            {
                currentModel.Status = "retired";
            }

            // Activate target version
            var targetModel = await _dbContext.ModelPerformance
                .FirstOrDefaultAsync(m => m.ModelVersion == targetVersion, ct);

            if (targetModel != null)
            {
                targetModel.Status = "production";
            }

            // Log rollback event
            var rollbackEvent = new ImprovementEvent
            {
                EventType = "model_rollback",
                ComponentType = "model",
                ComponentId = modelType,
                OldValue = JsonSerializer.SerializeToDocument(new { version = currentModel?.ModelVersion }),
                NewValue = JsonSerializer.SerializeToDocument(new { version = targetVersion }),
                TriggerReason = reason,
                Automated = true
            };

            _dbContext.ImprovementEvents.Add(rollbackEvent);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Rollback completed. {ModelType} now using {Version}", 
                modelType, targetVersion);
        }

        // Private helper methods
        private async Task<List<TrainingSample>> ExtractTrainingDataAsync(string modelType, int days, CancellationToken ct)
        {
            var startDate = DateTime.UtcNow.AddDays(-days);
            
            // Get trade outcomes with market conditions
            var samples = await _dbContext.TradeOutcomes
                .Where(t => t.EntryTime >= startDate && t.IsComplete)
                .Join(
                    _dbContext.MarketConditions,
                    trade => new { trade.Symbol, Time = trade.EntryTime },
                    market => new { market.Symbol, Time = market.Timestamp },
                    (trade, market) => new TrainingSample
                    {
                        TradeId = trade.TradeId,
                        Symbol = trade.Symbol,
                        Timestamp = trade.EntryTime,
                        Features = new Dictionary<string, float>
                        {
                            { "rsi", (float)(market.Rsi14 ?? 50) },
                            { "ema_dist", (float)((market.Close - market.Ema20 ?? market.Close) / market.Close * 100) },
                            { "atr", (float)(market.Atr14 ?? 0) },
                            { "volatility", (float)(market.Volatility20d ?? 0) },
                            { "volume_ratio", (float)(market.Volume / 1000000) },
                            { "bb_position", CalculateBBPosition(market) },
                            { "macd", (float)(market.MacdLine ?? 0) },
                            { "adx", 50 }, // Placeholder
                            { "hour", trade.EntryTime.Hour },
                            { "day_of_week", (int)trade.EntryTime.DayOfWeek }
                        },
                        Label = trade.IsWin ? 1 : 0,
                        Return = (float)(trade.NetPnl ?? 0),
                        Regime = market.DetectedRegime ?? "unknown"
                    })
                .ToListAsync(ct);

            return samples;
        }

        private float CalculateBBPosition(MarketConditions market)
        {
            if (!market.BbUpper.HasValue || !market.BbLower.HasValue || market.BbUpper == market.BbLower)
                return 0.5f;
            
            return (float)((market.Close - market.BbLower.Value) / 
                (market.BbUpper.Value - market.BbLower.Value));
        }

        private async Task<FeatureMatrix> PrepareFeaturesAsync(List<TrainingSample> samples, CancellationToken ct)
        {
            // Normalize features
            var normalizedSamples = new List<TrainingSample>();
            
            foreach (var sample in samples)
            {
                var normalized = new TrainingSample
                {
                    TradeId = sample.TradeId,
                    Symbol = sample.Symbol,
                    Timestamp = sample.Timestamp,
                    Features = new Dictionary<string, float>(),
                    Label = sample.Label,
                    Return = sample.Return,
                    Regime = sample.Regime
                };

                foreach (var feature in sample.Features)
                {
                    // Simple z-score normalization
                    var values = samples.Select(s => s.Features[feature.Key]).ToList();
                    var mean = values.Average();
                    var std = CalculateStdDev(values);
                    
                    normalized.Features[feature.Key] = std > 0 
                        ? (feature.Value - (float)mean) / (float)std 
                        : 0;
                }

                normalizedSamples.Add(normalized);
            }

            // Split train/test
            var splitIdx = (int)(normalizedSamples.Count * 0.8);
            
            return new FeatureMatrix
            {
                TrainSamples = normalizedSamples.Take(splitIdx).ToList(),
                TestSamples = normalizedSamples.Skip(splitIdx).ToList(),
                FeatureNames = normalizedSamples.First().Features.Keys.ToList()
            };
        }

        private async Task<TrainingResult> TrainModelAsync(string modelType, FeatureMatrix features, 
            string version, CancellationToken ct)
        {
            _logger.LogInformation("Training {ModelType} model with {TrainCount} samples", 
                modelType, features.TrainSamples.Count);

            // This would integrate with actual ML framework (ML.NET, TensorFlow, etc.)
            // For now, simulating training
            
            await Task.Delay(5000, ct); // Simulate training time

            // Save model artifacts
            var modelPath = Path.Combine(_modelRegistryPath, modelType, version);
            Directory.CreateDirectory(modelPath);
            
            // Save feature names and normalization params
            var metadata = new
            {
                version = version,
                model_type = modelType,
                feature_names = features.FeatureNames,
                training_date = DateTime.UtcNow,
                train_samples = features.TrainSamples.Count,
                test_samples = features.TestSamples.Count
            };
            
            await File.WriteAllTextAsync(
                Path.Combine(modelPath, "metadata.json"),
                JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }),
                ct);

            return new TrainingResult
            {
                ModelPath = modelPath,
                Version = version,
                TrainingSamples = features.TrainSamples.Count,
                FeatureCount = features.FeatureNames.Count
            };
        }

        private async Task<ModelEvaluation> EvaluateModelAsync(string modelType, string version, 
            TrainingResult training, CancellationToken ct)
        {
            // Load test data and evaluate
            // This is a simulated evaluation
            
            var random = new Random(version.GetHashCode());
            
            // Simulate metrics (in real implementation, use actual predictions)
            var accuracy = 0.55 + random.NextDouble() * 0.15; // 55-70%
            var precision = accuracy * (0.9 + random.NextDouble() * 0.1);
            var recall = accuracy * (0.85 + random.NextDouble() * 0.15);
            var f1 = 2 * (precision * recall) / (precision + recall);
            
            return new ModelEvaluation
            {
                Accuracy = accuracy,
                Precision = precision,
                Recall = recall,
                F1Score = f1,
                AucRoc = 0.6 + random.NextDouble() * 0.2,
                SharpeRatio = 1.0 + random.NextDouble() * 0.5,
                WinRate = accuracy,
                ExpectedImprovement = (accuracy - 0.55) / 0.55 // Improvement over baseline
            };
        }

        private async Task StoreModelPerformanceAsync(string modelType, string version, 
            ModelEvaluation evaluation, int trainingSamples, CancellationToken ct)
        {
            var performance = new ModelPerformance
            {
                ModelVersion = version,
                ModelType = modelType,
                EvaluationDate = DateTime.UtcNow.Date,
                TrainingSamples = trainingSamples,
                TestingSamples = (int)(trainingSamples * 0.2),
                TrainingPeriodStart = DateTime.UtcNow.AddDays(-90).Date,
                TrainingPeriodEnd = DateTime.UtcNow.Date,
                Accuracy = (decimal)evaluation.Accuracy,
                Precision = (decimal)evaluation.Precision,
                Recall = (decimal)evaluation.Recall,
                F1Score = (decimal)evaluation.F1Score,
                AucRoc = (decimal)evaluation.AucRoc,
                Status = "evaluating"
            };

            _dbContext.ModelPerformance.Add(performance);
            await _dbContext.SaveChangesAsync(ct);
        }

        private async Task StartABTestAsync(string modelType, string newVersion, CancellationToken ct)
        {
            // Get current production version
            var currentVersion = await _dbContext.ModelPerformance
                .Where(m => m.ModelType == modelType && m.Status == "production")
                .OrderByDescending(m => m.EvaluationDate)
                .Select(m => m.ModelVersion)
                .FirstOrDefaultAsync(ct);

            if (currentVersion == null)
            {
                // No current version, promote directly
                await PromoteModelAsync(newVersion, ct);
                return;
            }

            // Create A/B test
            var test = new ABTest
            {
                TestId = Guid.NewGuid(),
                TestName = $"{modelType}_v{currentVersion}_vs_v{newVersion}",
                ControlVersion = currentVersion,
                TreatmentVersion = newVersion,
                TestType = "model",
                ControlTrafficPercent = 90,
                TreatmentTrafficPercent = 10,
                StartedAt = DateTime.UtcNow,
                ScheduledDurationHours = 48,
                SuccessMetric = "sharpe_ratio",
                MinimumImprovementPercent = 5.0m,
                Status = "running"
            };

            _dbContext.ABTests.Add(test);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Started A/B test: {TestName}", test.TestName);
        }

        private string GenerateVersion()
        {
            var timestamp = DateTime.UtcNow;
            return $"{timestamp:yyyyMMdd}-{timestamp:HHmmss}";
        }

        private double CalculateStdDev(List<float> values)
        {
            if (values.Count < 2) return 0;
            var avg = values.Average();
            var sumSquares = values.Sum(v => (v - avg) * (v - avg));
            return Math.Sqrt(sumSquares / values.Count);
        }
    }

    // Supporting classes
    public class TrainingSample
    {
        public Guid TradeId { get; set; }
        public string Symbol { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, float> Features { get; set; }
        public int Label { get; set; } // 1 = win, 0 = loss
        public float Return { get; set; }
        public string Regime { get; set; }
    }

    public class FeatureMatrix
    {
        public List<TrainingSample> TrainSamples { get; set; }
        public List<TrainingSample> TestSamples { get; set; }
        public List<string> FeatureNames { get; set; }
    }

    public class TrainingResult
    {
        public string ModelPath { get; set; }
        public string Version { get; set; }
        public int TrainingSamples { get; set; }
        public int FeatureCount { get; set; }
    }

    public class ModelEvaluation
    {
        public double Accuracy { get; set; }
        public double Precision { get; set; }
        public double Recall { get; set; }
        public double F1Score { get; set; }
        public double AucRoc { get; set; }
        public double SharpeRatio { get; set; }
        public double WinRate { get; set; }
        public double ExpectedImprovement { get; set; }
    }
}
