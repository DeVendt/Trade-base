using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ImprovementEngine.Models;
using ImprovementEngine.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ImprovementEngine.Services
{
    /// <summary>
    /// Core engine that continuously analyzes performance and triggers improvements
    /// </summary>
    public class ContinuousImprovementEngine : BackgroundService
    {
        private readonly ImprovementDbContext _dbContext;
        private readonly DiscordNotifier _discord;
        private readonly ILogger<ContinuousImprovementEngine> _logger;
        private readonly ModelTrainingService _modelTraining;
        private readonly OptimizationService _optimization;
        private readonly ABTestService _abTesting;

        // Configuration
        private readonly TimeSpan _analysisInterval = TimeSpan.FromMinutes(15);
        private readonly TimeSpan _dailySummaryTime = new TimeSpan(21, 0, 0); // 9 PM UTC
        
        // Performance thresholds
        private readonly decimal _minSharpeRatio = 1.0m;
        private readonly decimal _maxDrawdownThreshold = 0.15m; // 15%
        private readonly decimal _minWinRate = 0.45m; // 45%
        private readonly decimal _performanceDegradationThreshold = 0.10m; // 10% decline

        public ContinuousImprovementEngine(
            ImprovementDbContext dbContext,
            DiscordNotifier discord,
            ILogger<ContinuousImprovementEngine> logger,
            ModelTrainingService modelTraining,
            OptimizationService optimization,
            ABTestService abTesting)
        {
            _dbContext = dbContext;
            _discord = discord;
            _logger = logger;
            _modelTraining = modelTraining;
            _optimization = optimization;
            _abTesting = abTesting;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 Continuous Improvement Engine started");
            
            await _discord.NotifyModelDeployedAsync(
                "ImprovementEngine", 
                "N/A", 
                0, 
                "N/A", 
                "Engine started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var cycleStart = DateTime.UtcNow;
                    
                    // 1. Process scheduled optimization tasks
                    await ProcessOptimizationQueueAsync(stoppingToken);
                    
                    // 2. Analyze recent performance
                    await AnalyzePerformanceAsync(stoppingToken);
                    
                    // 3. Check for model drift
                    await CheckModelDriftAsync(stoppingToken);
                    
                    // 4. Update A/B tests
                    await UpdateABTestsAsync(stoppingToken);
                    
                    // 5. Send daily summary if it's time
                    await SendDailySummaryIfNeededAsync(stoppingToken);
                    
                    // 6. Validate pending predictions
                    await ValidatePendingPredictionsAsync(stoppingToken);

                    var cycleDuration = DateTime.UtcNow - cycleStart;
                    _logger.LogInformation("Improvement cycle completed in {DurationMs}ms", cycleDuration.TotalMilliseconds);
                    
                    // Wait for next cycle
                    await Task.Delay(_analysisInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Improvement Engine shutting down...");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in improvement cycle");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        /// <summary>
        /// Process scheduled optimization tasks from the queue
        /// </summary>
        private async Task ProcessOptimizationQueueAsync(CancellationToken ct)
        {
            var pendingTasks = await _dbContext.OptimizationQueue
                .Where(t => t.Enabled && t.Status == "pending" && t.NextRunAt <= DateTime.UtcNow)
                .OrderBy(t => t.Priority)
                .ThenBy(t => t.NextRunAt)
                .Take(5) // Process max 5 per cycle
                .ToListAsync(ct);

            foreach (var task in pendingTasks)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    _logger.LogInformation("Processing optimization task: {TaskType} for {Component}",
                        task.TaskType, task.ComponentId);

                    task.Status = "running";
                    await _dbContext.SaveChangesAsync(ct);

                    var result = await ExecuteOptimizationTaskAsync(task, ct);
                    
                    task.MarkCompleted(JsonSerializer.SerializeToDocument(result));
                    
                    await _discord.NotifyOptimizationCompleteAsync(
                        task.TaskType,
                        task.ComponentId,
                        result.ImprovementPercent,
                        result.Details);

                    _logger.LogInformation("Optimization completed: {TaskType} improved by {Improvement:F2}%",
                        task.TaskType, result.ImprovementPercent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Optimization task failed: {TaskType}", task.TaskType);
                    task.MarkFailed(ex.Message);

                    if (task.HasFailedRepeatedly(3))
                    {
                        task.Enabled = false;
                        await _discord.NotifyCriticalAlertAsync(
                            "OptimizationQueue",
                            $"Task {task.TaskType} failed 3 times: {ex.Message}",
                            "Optimization task disabled. Manual intervention required.");
                    }
                }

                await _dbContext.SaveChangesAsync(ct);
            }
        }

        /// <summary>
        /// Execute a specific optimization task
        /// </summary>
        private async Task<OptimizationResult> ExecuteOptimizationTaskAsync(OptimizationQueue task, CancellationToken ct)
        {
            return task.TaskType.ToLower() switch
            {
                "hyperparameter" => await _optimization.OptimizeHyperparametersAsync(task.ComponentId, ct),
                "feature_selection" => await _optimization.OptimizeFeatureSelectionAsync(task.ComponentId, ct),
                "strategy_weights" => await _optimization.OptimizeStrategyWeightsAsync(ct),
                "threshold_tuning" => await _optimization.OptimizeThresholdsAsync(task.ComponentId, ct),
                "model_retrain" => await _modelTraining.RetrainModelAsync(task.ComponentId, ct),
                _ => throw new NotSupportedException($"Unknown task type: {task.TaskType}")
            };
        }

        /// <summary>
        /// Analyze recent trading performance and trigger actions
        /// </summary>
        private async Task AnalyzePerformanceAsync(CancellationToken ct)
        {
            var last24Hours = DateTime.UtcNow.AddHours(-24);
            
            // Get performance by strategy
            var strategyPerformance = await _dbContext.TradeOutcomes
                .Where(t => t.EntryTime >= last24Hours && t.IsComplete)
                .GroupBy(t => t.StrategyId)
                .Select(g => new
                {
                    StrategyId = g.Key,
                    TotalTrades = g.Count(),
                    WinRate = g.Count(t => t.IsWin) / (decimal)g.Count(),
                    NetPnl = g.Sum(t => t.NetPnl ?? 0),
                    AvgTrade = g.Average(t => t.NetPnl ?? 0)
                })
                .ToListAsync(ct);

            foreach (var perf in strategyPerformance)
            {
                // Check for underperforming strategies
                if (perf.WinRate < _minWinRate && perf.TotalTrades >= 10)
                {
                    _logger.LogWarning("Strategy {Strategy} win rate {WinRate:P1} below threshold", 
                        perf.StrategyId, perf.WinRate);

                    await _discord.NotifyPerformanceAlertAsync(
                        perf.StrategyId,
                        "Win Rate",
                        perf.WinRate,
                        _minWinRate,
                        "Scheduled for hyperparameter optimization");

                    // Queue optimization
                    await QueueOptimizationAsync("hyperparameter", perf.StrategyId, priority: 2);
                }
            }

            // Calculate rolling Sharpe ratio
            var recentPerformance = await CalculateRollingSharpeRatioAsync(30, ct);
            if (recentPerformance < _minSharpeRatio)
            {
                _logger.LogWarning("Portfolio Sharpe ratio {Sharpe:F2} below threshold", recentPerformance);
                
                await _discord.NotifyPerformanceAlertAsync(
                    "Portfolio",
                    "Sharpe Ratio",
                    recentPerformance,
                    _minSharpeRatio,
                    "Triggering model retraining");

                await QueueOptimizationAsync("model_retrain", "direction_model", priority: 1);
            }
        }

        /// <summary>
        /// Check for model drift and predictive degradation
        /// </summary>
        private async Task CheckModelDriftAsync(CancellationToken ct)
        {
            var lastWeek = DateTime.UtcNow.AddDays(-7);
            
            // Get accuracy by model version over time
            var accuracyByVersion = await _dbContext.ModelPredictions
                .Where(p => p.PredictedAt >= lastWeek && p.PredictionAccuracy.HasValue)
                .GroupBy(p => p.ModelVersion)
                .Select(g => new
                {
                    Version = g.Key,
                    Accuracy = g.Average(p => p.PredictionAccuracy.Value ? 1.0 : 0.0),
                    Count = g.Count()
                })
                .ToListAsync(ct);

            var currentVersion = accuracyByVersion.OrderByDescending(v => v.Count).FirstOrDefault();
            
            if (currentVersion != null && currentVersion.Count >= 100)
            {
                var expectedAccuracy = 0.55; // Baseline expectation
                var drift = (currentVersion.Accuracy - expectedAccuracy) / expectedAccuracy;

                if (drift < -_performanceDegradationThreshold)
                {
                    _logger.LogWarning("Model drift detected: accuracy dropped {Drift:P1}", drift);
                    
                    await _discord.NotifyPerformanceAlertAsync(
                        $"Model {currentVersion.Version}",
                        "Prediction Accuracy",
                        (decimal)currentVersion.Accuracy,
                        (decimal)expectedAccuracy,
                        "Triggering emergency retraining with expanded dataset");

                    await QueueOptimizationAsync("model_retrain", "direction_model", priority: 1);
                    await QueueOptimizationAsync("feature_selection", "direction_model", priority: 2);
                }
            }
        }

        /// <summary>
        /// Update running A/B tests and promote winners
        /// </summary>
        private async Task UpdateABTestsAsync(CancellationToken ct)
        {
            var activeTests = await _dbContext.ABTests
                .Where(t => t.Status == "running")
                .ToListAsync(ct);

            foreach (var test in activeTests)
            {
                var duration = DateTime.UtcNow - test.StartedAt;
                
                // Calculate current metrics
                var controlMetrics = await CalculateTestMetricsAsync(test.ControlVersion, test.StartedAt, ct);
                var treatmentMetrics = await CalculateTestMetricsAsync(test.TreatmentVersion, test.StartedAt, ct);

                test.ControlMetrics = JsonSerializer.SerializeToDocument(controlMetrics);
                test.TreatmentMetrics = JsonSerializer.SerializeToDocument(treatmentMetrics);

                // Check if test should conclude
                bool shouldConclude = duration.TotalHours >= test.ScheduledDurationHours;
                
                if (controlMetrics.SharpeRatio > 0 && treatmentMetrics.SharpeRatio > 0)
                {
                    var improvement = (treatmentMetrics.SharpeRatio - controlMetrics.SharpeRatio) 
                        / controlMetrics.SharpeRatio * 100;
                    test.ImprovementPercent = improvement;

                    // Early promotion if treatment is clearly better
                    if (improvement >= test.MinimumImprovementPercent && duration.TotalHours >= 24)
                    {
                        shouldConclude = true;
                    }
                    // Early rejection if treatment is clearly worse
                    else if (improvement < -10 && duration.TotalHours >= 12)
                    {
                        shouldConclude = true;
                    }
                }

                if (shouldConclude)
                {
                    await ConcludeABTestAsync(test, controlMetrics, treatmentMetrics, ct);
                }

                await _dbContext.SaveChangesAsync(ct);
            }
        }

        /// <summary>
        /// Conclude an A/B test and take action
        /// </summary>
        private async Task ConcludeABTestAsync(ABTest test, PerformanceMetrics control, 
            PerformanceMetrics treatment, CancellationToken ct)
        {
            test.EndedAt = DateTime.UtcNow;
            
            var winner = test.ImprovementPercent >= test.MinimumImprovementPercent 
                ? test.TreatmentVersion 
                : test.ControlVersion;
            
            test.Status = winner == test.TreatmentVersion ? "promoted" : "rejected";
            test.Conclusion = $"Treatment {(winner == test.TreatmentVersion ? "promoted" : "rejected")} " +
                $"with {test.ImprovementPercent:F2}% improvement";

            // Deploy if treatment won
            if (winner == test.TreatmentVersion)
            {
                await _modelTraining.PromoteModelAsync(test.TreatmentVersion, ct);
                
                await _discord.NotifyABTestResultAsync(
                    test.TestName,
                    test.ControlVersion,
                    test.TreatmentVersion,
                    winner,
                    test.ImprovementPercent,
                    "Treatment promoted to production");
            }
            else
            {
                await _discord.NotifyABTestResultAsync(
                    test.TestName,
                    test.ControlVersion,
                    test.TreatmentVersion,
                    winner,
                    test.ImprovementPercent,
                    "Treatment rejected, keeping control");
            }

            // Log improvement event
            var improvementEvent = new ImprovementEvent
            {
                EventType = "ab_test_concluded",
                ComponentType = "model",
                ComponentId = test.TestName,
                TriggerReason = "ab_test_complete",
                PerformanceBefore = JsonSerializer.SerializeToDocument(control),
                PerformanceAfter = JsonSerializer.SerializeToDocument(treatment),
                ImprovementMetrics = JsonSerializer.SerializeToDocument(new { 
                    improvement_percent = test.ImprovementPercent,
                    winner = winner
                }),
                Automated = true
            };
            
            _dbContext.ImprovementEvents.Add(improvementEvent);
        }

        /// <summary>
        /// Send daily summary at scheduled time
        /// </summary>
        private async Task SendDailySummaryIfNeededAsync(CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var todaySummaryTime = new DateTime(now.Year, now.Month, now.Day, 
                _dailySummaryTime.Hours, _dailySummaryTime.Minutes, 0, DateTimeKind.Utc);
            
            if (now < todaySummaryTime) return;

            // Check if already sent today
            var lastSummary = await _dbContext.ImprovementEvents
                .Where(e => e.EventType == "daily_summary")
                .OrderByDescending(e => e.EventTimestamp)
                .FirstOrDefaultAsync(ct);

            if (lastSummary?.EventTimestamp.Date == now.Date) return;

            // Generate summary
            var today = now.Date;
            var todayTrades = await _dbContext.TradeOutcomes
                .Where(t => t.EntryTime.Date == today && t.IsComplete)
                .ToListAsync(ct);

            var totalPnl = todayTrades.Sum(t => t.NetPnl ?? 0);
            var winRate = todayTrades.Any() 
                ? (decimal)todayTrades.Count(t => t.IsWin) / todayTrades.Count 
                : 0;

            var bestStrategy = todayTrades
                .GroupBy(t => t.StrategyId)
                .Select(g => new { Strategy = g.Key, Pnl = g.Sum(t => t.NetPnl ?? 0) })
                .OrderByDescending(x => x.Pnl)
                .FirstOrDefault()?.Strategy ?? "N/A";

            var modelAccuracy = await _dbContext.ModelPredictions
                .Where(p => p.PredictedAt.Date == today && p.PredictionAccuracy.HasValue)
                .AverageAsync(p => p.PredictionAccuracy.Value ? 1.0 : 0.0, ct);

            await _discord.NotifyDailySummaryAsync(
                todayTrades.Count,
                totalPnl,
                winRate,
                bestStrategy,
                $"{modelAccuracy:P1}");

            // Log that summary was sent
            _dbContext.ImprovementEvents.Add(new ImprovementEvent
            {
                EventType = "daily_summary",
                ComponentType = "system",
                ComponentId = "daily_report",
                Automated = true,
                DiscordNotified = true
            });

            await _dbContext.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Validate pending predictions with actual outcomes
        /// </summary>
        private async Task ValidatePendingPredictionsAsync(CancellationToken ct)
        {
            var pendingPredictions = await _dbContext.ModelPredictions
                .Where(p => !p.ValidatedAt.HasValue && 
                    p.PredictedAt < DateTime.UtcNow.AddMinutes(-p.PredictionHorizonMinutes))
                .Take(1000) // Process in batches
                .ToListAsync(ct);

            foreach (var prediction in pendingPredictions)
            {
                var actualPrice = await GetActualPriceAsync(
                    prediction.Symbol, 
                    prediction.PredictedAt.AddMinutes(prediction.PredictionHorizonMinutes), 
                    ct);
                
                if (actualPrice.HasValue)
                {
                    // Calculate actual return
                    var features = prediction.FeatureVector;
                    var entryPrice = features.RootElement.GetProperty("close").GetDecimal();
                    var actualReturn = (actualPrice.Value - entryPrice) / entryPrice;
                    var actualDirection = actualReturn > 0.001m ? "LONG" : 
                                         actualReturn < -0.001m ? "SHORT" : "NEUTRAL";

                    prediction.Validate(actualDirection, actualReturn);
                }
            }

            if (pendingPredictions.Any())
            {
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("Validated {Count} predictions", pendingPredictions.Count);
            }
        }

        // Helper methods
        private async Task QueueOptimizationAsync(string taskType, string componentId, int priority = 5)
        {
            var existing = await _dbContext.OptimizationQueue
                .FirstOrDefaultAsync(t => t.TaskType == taskType && 
                    t.ComponentId == componentId && 
                    t.Status == "pending");

            if (existing == null)
            {
                _dbContext.OptimizationQueue.Add(new OptimizationQueue
                {
                    TaskType = taskType,
                    ComponentId = componentId,
                    Frequency = "daily",
                    NextRunAt = DateTime.UtcNow.AddMinutes(5), // Start soon
                    Priority = priority,
                    Config = JsonSerializer.SerializeToDocument(new { })
                });

                await _dbContext.SaveChangesAsync();
            }
        }

        private async Task<decimal> CalculateRollingSharpeRatioAsync(int days, CancellationToken ct)
        {
            var startDate = DateTime.UtcNow.AddDays(-days);
            var dailyReturns = await _dbContext.TradeOutcomes
                .Where(t => t.EntryTime >= startDate && t.IsComplete)
                .GroupBy(t => t.EntryTime.Date)
                .Select(g => g.Sum(t => t.NetPnl ?? 0))
                .ToListAsync(ct);

            if (dailyReturns.Count < 10) return 0;

            var avgReturn = dailyReturns.Average();
            var stdDev = CalculateStandardDeviation(dailyReturns);
            
            return stdDev > 0 ? avgReturn / stdDev * (decimal)Math.Sqrt(252) : 0; // Annualized
        }

        private async Task<PerformanceMetrics> CalculateTestMetricsAsync(string modelVersion, 
            DateTime startAt, CancellationToken ct)
        {
            var trades = await _dbContext.TradeOutcomes
                .Where(t => t.ModelVersion == modelVersion && t.EntryTime >= startAt && t.IsComplete)
                .ToListAsync(ct);

            if (!trades.Any()) return new PerformanceMetrics();

            var returns = trades.Select(t => t.NetPnl ?? 0).ToList();
            var avgReturn = returns.Average();
            var stdDev = CalculateStandardDeviation(returns);

            return new PerformanceMetrics
            {
                TotalTrades = trades.Count,
                WinRate = (decimal)trades.Count(t => t.IsWin) / trades.Count,
                NetPnl = returns.Sum(),
                SharpeRatio = stdDev > 0 ? avgReturn / stdDev * (decimal)Math.Sqrt(trades.Count) : 0,
                ProfitFactor = Math.Abs(trades.Where(t => t.NetPnl > 0).Sum(t => t.NetPnl ?? 0) /
                    trades.Where(t => t.NetPnl < 0).Sum(t => Math.Abs(t.NetPnl ?? 0)))
            };
        }

        private async Task<decimal?> GetActualPriceAsync(string symbol, DateTime timestamp, CancellationToken ct)
        {
            var condition = await _dbContext.MarketConditions
                .FirstOrDefaultAsync(m => m.Symbol == symbol && m.Timestamp <= timestamp, ct);
            
            return condition?.Close;
        }

        private decimal CalculateStandardDeviation(List<decimal> values)
        {
            if (values.Count < 2) return 0;
            var avg = values.Average();
            var sumSquares = values.Sum(v => (v - avg) * (v - avg));
            return (decimal)Math.Sqrt((double)(sumSquares / values.Count));
        }
    }

    // Supporting classes
    public class OptimizationResult
    {
        public decimal ImprovementPercent { get; set; }
        public string Details { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    public class PerformanceMetrics
    {
        public int TotalTrades { get; set; }
        public decimal WinRate { get; set; }
        public decimal NetPnl { get; set; }
        public decimal SharpeRatio { get; set; }
        public decimal ProfitFactor { get; set; }
    }
}
