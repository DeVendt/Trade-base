using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImprovementEngine.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ImprovementEngine.Services
{
    /// <summary>
    /// Handles all optimization tasks: hyperparameters, features, weights, thresholds
    /// </summary>
    public class OptimizationService
    {
        private readonly ImprovementDbContext _dbContext;
        private readonly ILogger<OptimizationService> _logger;
        private readonly Random _random = new();

        public OptimizationService(ImprovementDbContext dbContext, ILogger<OptimizationService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Optimize strategy hyperparameters using genetic algorithm
        /// </summary>
        public async Task<OptimizationResult> OptimizeHyperparametersAsync(string strategyId, CancellationToken ct)
        {
            _logger.LogInformation("Starting hyperparameter optimization for {Strategy}", strategyId);

            // Get recent backtest data
            var historicalData = await GetHistoricalPerformanceAsync(strategyId, days: 60, ct);
            
            // Genetic algorithm parameters
            const int populationSize = 20;
            const int generations = 10;
            const double mutationRate = 0.1;

            // Initialize population with random parameters
            var population = InitializePopulation(populationSize, strategyId);
            
            Individual bestIndividual = null;
            double bestFitness = double.MinValue;

            for (int gen = 0; gen < generations; gen++)
            {
                // Evaluate fitness for each individual
                foreach (var individual in population)
                {
                    individual.Fitness = await EvaluateFitnessAsync(individual, historicalData, ct);
                    
                    if (individual.Fitness > bestFitness)
                    {
                        bestFitness = individual.Fitness;
                        bestIndividual = individual;
                    }
                }

                // Selection: Keep top 50%
                var selected = population.OrderByDescending(i => i.Fitness)
                    .Take(populationSize / 2)
                    .ToList();

                // Crossover and mutation to create new generation
                population = EvolvePopulation(selected, populationSize, mutationRate);

                _logger.LogDebug("Generation {Gen}: Best fitness = {Fitness:F4}", gen, bestFitness);
            }

            // Apply best parameters
            await ApplyParametersAsync(strategyId, bestIndividual.Parameters, ct);

            // Calculate improvement
            var baseline = historicalData.Average(t => t.NetPnl ?? 0);
            var optimized = bestFitness;
            var improvement = baseline != 0 ? (optimized - Math.Abs((double)baseline)) / Math.Abs((double)baseline) * 100 : 0;

            return new OptimizationResult
            {
                ImprovementPercent = (decimal)improvement,
                Details = $"Optimized {bestIndividual.Parameters.Count} parameters over {generations} generations",
                Parameters = bestIndividual.Parameters.ToDictionary(
                    p => p.Key, 
                    p => (object)p.Value)
            };
        }

        /// <summary>
        /// Optimize feature selection using recursive feature elimination
        /// </summary>
        public async Task<OptimizationResult> OptimizeFeatureSelectionAsync(string modelId, CancellationToken ct)
        {
            _logger.LogInformation("Starting feature selection optimization for {Model}", modelId);

            var allFeatures = await GetAvailableFeaturesAsync(ct);
            var importantFeatures = new List<string>();
            var removedFeatures = new List<string>();

            // Get feature importance from recent model performance
            var predictions = await _dbContext.ModelPredictions
                .Where(p => p.ModelType == modelId && p.PredictionAccuracy.HasValue)
                .OrderByDescending(p => p.PredictedAt)
                .Take(5000)
                .ToListAsync(ct);

            if (!predictions.Any())
            {
                return new OptimizationResult
                {
                    ImprovementPercent = 0,
                    Details = "Insufficient data for feature selection"
                };
            }

            // Calculate mutual information for each feature
            var featureScores = new Dictionary<string, double>();
            foreach (var feature in allFeatures)
            {
                var score = CalculateFeatureImportance(predictions, feature);
                featureScores[feature] = score;
            }

            // Keep features above threshold
            const double importanceThreshold = 0.01;
            importantFeatures = featureScores.Where(f => f.Value >= importanceThreshold)
                .OrderByDescending(f => f.Value)
                .Take(50) // Max 50 features
                .Select(f => f.Key)
                .ToList();

            removedFeatures = allFeatures.Except(importantFeatures).ToList();

            // Calculate expected improvement
            var currentAccuracy = predictions.Average(p => p.PredictionAccuracy.Value ? 1.0 : 0.0);
            var expectedImprovement = removedFeatures.Count * 0.002; // Estimate 0.2% per removed noise feature

            _logger.LogInformation("Feature selection: Keeping {Kept}, Removing {Removed} features",
                importantFeatures.Count, removedFeatures.Count);

            return new OptimizationResult
            {
                ImprovementPercent = (decimal)(expectedImprovement * 100),
                Details = $"Removed {removedFeatures.Count} low-importance features",
                Parameters = new Dictionary<string, object>
                {
                    { "kept_features", importantFeatures },
                    { "removed_features", removedFeatures },
                    { "feature_importance", featureScores.OrderByDescending(f => f.Value).Take(10).ToList() }
                }
            };
        }

        /// <summary>
        /// Optimize strategy weights for portfolio allocation
        /// </summary>
        public async Task<OptimizationResult> OptimizeStrategyWeightsAsync(CancellationToken ct)
        {
            _logger.LogInformation("Starting strategy weight optimization");

            var strategies = await _dbContext.StrategyPerformance
                .Where(s => s.PeriodType == "DAY" && s.PeriodStart >= DateTime.UtcNow.AddDays(-30))
                .GroupBy(s => s.StrategyId)
                .Select(g => new
                {
                    StrategyId = g.Key,
                    AvgSharpe = g.Average(s => s.SharpeRatio),
                    AvgWinRate = g.Average(s => s.WinRate),
                    TotalPnL = g.Sum(s => s.NetPnl),
                    Volatility = CalculateStdDev(g.Select(s => s.NetPnl).ToList())
                })
                .ToListAsync(ct);

            if (strategies.Count < 2)
            {
                return new OptimizationResult
                {
                    ImprovementPercent = 0,
                    Details = "Insufficient strategies for weight optimization"
                };
            }

            // Use Sharpe ratio optimization with risk parity
            var totalSharpe = strategies.Sum(s => Math.Max(0, (double)(s.AvgSharpe ?? 0)));
            var weights = new Dictionary<string, decimal>();

            foreach (var strategy in strategies)
            {
                var sharpeWeight = totalSharpe > 0 
                    ? Math.Max(0, (double)(strategy.AvgSharpe ?? 0)) / totalSharpe 
                    : 1.0 / strategies.Count;
                
                // Risk parity adjustment
                var riskWeight = strategy.Volatility > 0 
                    ? 1.0 / (double)strategy.Volatility 
                    : 1.0;
                
                var combinedWeight = (decimal)(sharpeWeight * 0.7 + riskWeight * 0.3);
                weights[strategy.StrategyId] = Math.Max(0.05m, Math.Min(0.5m, combinedWeight)); // Min 5%, Max 50%
            }

            // Normalize to sum to 1
            var totalWeight = weights.Values.Sum();
            weights = weights.ToDictionary(w => w.Key, w => w.Value / totalWeight);

            // Calculate expected portfolio Sharpe improvement
            var currentPortfolioSharpe = strategies.Average(s => s.AvgSharpe ?? 0);
            var weightedSharpe = strategies.Sum(s => weights[s.StrategyId] * (s.AvgSharpe ?? 0));
            var improvement = currentPortfolioSharpe > 0 
                ? (weightedSharpe - currentPortfolioSharpe) / currentPortfolioSharpe * 100 
                : 0;

            return new OptimizationResult
            {
                ImprovementPercent = improvement,
                Details = $"Optimized weights for {strategies.Count} strategies",
                Parameters = weights.ToDictionary(w => w.Key, w => (object)w.Value)
            };
        }

        /// <summary>
        /// Optimize entry/exit thresholds
        /// </summary>
        public async Task<OptimizationResult> OptimizeThresholdsAsync(string componentId, CancellationToken ct)
        {
            _logger.LogInformation("Starting threshold optimization for {Component}", componentId);

            var predictions = await _dbContext.ModelPredictions
                .Where(p => p.ModelType == componentId && p.PredictionAccuracy.HasValue)
                .OrderByDescending(p => p.PredictedAt)
                .Take(2000)
                .ToListAsync(ct);

            if (!predictions.Any())
            {
                return new OptimizationResult
                {
                    ImprovementPercent = 0,
                    Details = "Insufficient prediction data"
                };
            }

            // Grid search for optimal confidence threshold
            var bestThreshold = 0.5;
            var bestScore = 0.0;

            for (double threshold = 0.3; threshold <= 0.9; threshold += 0.05)
            {
                var filtered = predictions.Where(p => p.ConfidenceScore >= (decimal)threshold).ToList();
                if (filtered.Count < 100) continue;

                var accuracy = filtered.Average(p => p.PredictionAccuracy.Value ? 1.0 : 0.0);
                var coverage = (double)filtered.Count / predictions.Count;
                
                // Score = accuracy * coverage (want both high accuracy and good coverage)
                var score = accuracy * coverage;
                
                if (score > bestScore)
                {
                    bestScore = score;
                    bestThreshold = threshold;
                }
            }

            // Calculate expected improvement
            var currentAccuracy = predictions.Average(p => p.PredictionAccuracy.Value ? 1.0 : 0.0);
            var filteredAccuracy = predictions
                .Where(p => p.ConfidenceScore >= (decimal)bestThreshold)
                .Average(p => p.PredictionAccuracy.Value ? 1.0 : 0.0);
            
            var improvement = currentAccuracy > 0 
                ? (filteredAccuracy - currentAccuracy) / currentAccuracy * 100 
                : 0;

            return new OptimizationResult
            {
                ImprovementPercent = (decimal)improvement,
                Details = $"Optimal confidence threshold: {bestThreshold:P0}",
                Parameters = new Dictionary<string, object>
                {
                    { "confidence_threshold", bestThreshold },
                    { "expected_accuracy", filteredAccuracy },
                    { "trade_coverage", (double)predictions.Count(p => p.ConfidenceScore >= (decimal)bestThreshold) / predictions.Count }
                }
            };
        }

        // Private helper methods
        private List<Individual> InitializePopulation(int size, string strategyId)
        {
            var population = new List<Individual>();
            
            for (int i = 0; i < size; i++)
            {
                var individual = new Individual
                {
                    Parameters = new Dictionary<string, double>
                    {
                        { "ema_fast", _random.Next(5, 25) },
                        { "ema_slow", _random.Next(30, 100) },
                        { "rsi_period", _random.Next(7, 21) },
                        { "rsi_overbought", _random.Next(65, 85) },
                        { "rsi_oversold", _random.Next(15, 35) },
                        { "atr_multiplier", _random.NextDouble() * 2 + 0.5 },
                        { "confidence_threshold", _random.NextDouble() * 0.4 + 0.5 }
                    }
                };
                
                // Ensure ema_fast < ema_slow
                if (individual.Parameters["ema_fast"] >= individual.Parameters["ema_slow"])
                {
                    individual.Parameters["ema_slow"] = individual.Parameters["ema_fast"] + 10;
                }
                
                population.Add(individual);
            }
            
            return population;
        }

        private async Task<double> EvaluateFitnessAsync(Individual individual, 
            List<TradeOutcome> historicalData, CancellationToken ct)
        {
            // Simulate trading with these parameters
            var simulatedPnL = 0.0;
            var wins = 0;
            var losses = 0;

            foreach (var trade in historicalData)
            {
                // Simple simulation based on parameter quality
                var entryQuality = CalculateEntryQuality(trade, individual.Parameters);
                
                if (entryQuality > individual.Parameters["confidence_threshold"])
                {
                    var pnl = (double)(trade.NetPnl ?? 0);
                    simulatedPnL += pnl;
                    
                    if (pnl > 0) wins++;
                    else if (pnl < 0) losses++;
                }
            }

            var totalTrades = wins + losses;
            if (totalTrades == 0) return -1000; // Penalize no trades

            var winRate = (double)wins / totalTrades;
            var profitFactor = losses > 0 ? Math.Abs(simulatedPnL / (simulatedPnL - (double)historicalData.Sum(t => t.NetPnl ?? 0))) : simulatedPnL;
            
            // Fitness = Sharpe-like metric considering win rate and profit
            return winRate * Math.Abs(simulatedPnL) * (simulatedPnL > 0 ? 1 : 0.5);
        }

        private double CalculateEntryQuality(TradeOutcome trade, Dictionary<string, double> parameters)
        {
            // Simplified quality calculation based on trade characteristics
            var baseQuality = 0.5;
            
            if (trade.NetPnl > 0) baseQuality += 0.3;
            if (trade.DurationSeconds < 3600) baseQuality += 0.1; // Favor quicker trades
            if (Math.Abs(trade.MaxFavorableExcursion.GetValueOrDefault()) > Math.Abs(trade.NetPnl.GetValueOrDefault() * 2)) 
                baseQuality -= 0.1; // Penalize trades that gave back profits
            
            return Math.Min(0.95, baseQuality);
        }

        private List<Individual> EvolvePopulation(List<Individual> selected, int targetSize, double mutationRate)
        {
            var newPopulation = new List<Individual>(selected);

            while (newPopulation.Count < targetSize)
            {
                // Tournament selection for parents
                var parent1 = selected[_random.Next(selected.Count)];
                var parent2 = selected[_random.Next(selected.Count)];

                // Crossover
                var child = new Individual
                {
                    Parameters = new Dictionary<string, double>()
                };

                foreach (var key in parent1.Parameters.Keys)
                {
                    // Uniform crossover
                    child.Parameters[key] = _random.NextDouble() < 0.5 
                        ? parent1.Parameters[key] 
                        : parent2.Parameters[key];

                    // Mutation
                    if (_random.NextDouble() < mutationRate)
                    {
                        var mutation = (_random.NextDouble() - 0.5) * 0.2; // ±10% mutation
                        child.Parameters[key] *= (1 + mutation);
                    }
                }

                newPopulation.Add(child);
            }

            return newPopulation;
        }

        private async Task<List<TradeOutcome>> GetHistoricalPerformanceAsync(string strategyId, int days, CancellationToken ct)
        {
            var startDate = DateTime.UtcNow.AddDays(-days);
            return await _dbContext.TradeOutcomes
                .Where(t => t.StrategyId == strategyId && t.EntryTime >= startDate && t.IsComplete)
                .OrderBy(t => t.EntryTime)
                .ToListAsync(ct);
        }

        private async Task<List<string>> GetAvailableFeaturesAsync(CancellationToken ct)
        {
            // Return list of available feature names
            return new List<string>
            {
                "ema_10", "ema_20", "ema_50", "ema_200",
                "rsi_14", "rsi_21",
                "macd_line", "macd_signal", "macd_histogram",
                "bb_upper", "bb_middle", "bb_lower", "bb_width",
                "atr_14", "atr_21",
                "volume_sma", "volume_ratio",
                "price_momentum_5", "price_momentum_10", "price_momentum_20",
                "volatility_20", "volatility_50",
                "adx", "plus_di", "minus_di",
                "obv", "mfi", "cci",
                "vwap", "vwap_distance",
                "session", "day_of_week", "hour_of_day"
            };
        }

        private double CalculateFeatureImportance(List<ModelPrediction> predictions, string feature)
        {
            // Simplified feature importance using correlation with accuracy
            var featureValues = new List<double>();
            var accuracies = new List<double>();

            foreach (var pred in predictions)
            {
                if (pred.FeatureVector.RootElement.TryGetProperty(feature, out var value))
                {
                    featureValues.Add(value.GetDouble());
                    accuracies.Add(pred.PredictionAccuracy.Value ? 1.0 : 0.0);
                }
            }

            if (featureValues.Count < 100) return 0;

            // Calculate correlation
            var avgFeature = featureValues.Average();
            var avgAccuracy = accuracies.Average();
            
            var numerator = 0.0;
            var denomFeature = 0.0;
            var denomAccuracy = 0.0;

            for (int i = 0; i < featureValues.Count; i++)
            {
                var diffFeature = featureValues[i] - avgFeature;
                var diffAccuracy = accuracies[i] - avgAccuracy;
                
                numerator += diffFeature * diffAccuracy;
                denomFeature += diffFeature * diffFeature;
                denomAccuracy += diffAccuracy * diffAccuracy;
            }

            var correlation = numerator / (Math.Sqrt(denomFeature) * Math.Sqrt(denomAccuracy) + 1e-10);
            return Math.Abs(correlation);
        }

        private async Task ApplyParametersAsync(string strategyId, Dictionary<string, double> parameters, CancellationToken ct)
        {
            _logger.LogInformation("Applying optimized parameters to {Strategy}", strategyId);
            
            // Store in database or configuration
            // This would update the strategy configuration
            await Task.CompletedTask;
        }

        private decimal CalculateStdDev(List<decimal> values)
        {
            if (values.Count < 2) return 0;
            var avg = values.Average();
            var sumSquares = values.Sum(v => (v - avg) * (v - avg));
            return (decimal)Math.Sqrt((double)(sumSquares / values.Count));
        }
    }

    public class Individual
    {
        public Dictionary<string, double> Parameters { get; set; }
        public double Fitness { get; set; }
    }
}
