using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImprovementEngine.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ImprovementEngine.Services
{
    /// <summary>
    /// Manages A/B testing for models and strategies
    /// </summary>
    public class ABTestService
    {
        private readonly ImprovementDbContext _dbContext;
        private readonly ILogger<ABTestService> _logger;

        public ABTestService(ImprovementDbContext dbContext, ILogger<ABTestService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Get the appropriate model version for a trade based on A/B test assignment
        /// </summary>
        public async Task<string> GetModelVersionForTradeAsync(string baseModelType, string symbol, CancellationToken ct)
        {
            // Check for active A/B tests for this model type
            var activeTest = await _dbContext.ABTests
                .Where(t => t.Status == "running" && 
                    (t.TestType == "model" || t.ControlVersion.StartsWith(baseModelType)))
                .OrderByDescending(t => t.StartedAt)
                .FirstOrDefaultAsync(ct);

            if (activeTest == null)
            {
                // No A/B test, return production version
                return await GetProductionVersionAsync(baseModelType, ct);
            }

            // Deterministic assignment based on symbol hash for consistency
            var hash = symbol.GetHashCode();
            var assignment = (hash % 100 + 100) % 100; // Ensure positive

            return assignment < activeTest.TreatmentTrafficPercent 
                ? activeTest.TreatmentVersion 
                : activeTest.ControlVersion;
        }

        /// <summary>
        /// Create a new A/B test
        /// </summary>
        public async Task<ABTest> CreateTestAsync(
            string testName,
            string testType,
            string controlVersion,
            string treatmentVersion,
            int treatmentPercent,
            string successMetric,
            int durationHours,
            CancellationToken ct)
        {
            _logger.LogInformation("Creating A/B test: {TestName}", testName);

            var test = new ABTest
            {
                TestId = Guid.NewGuid(),
                TestName = testName,
                TestType = testType,
                ControlVersion = controlVersion,
                TreatmentVersion = treatmentVersion,
                ControlTrafficPercent = 100 - treatmentPercent,
                TreatmentTrafficPercent = treatmentPercent,
                StartedAt = DateTime.UtcNow,
                ScheduledDurationHours = durationHours,
                SuccessMetric = successMetric,
                MinimumImprovementPercent = 5.0m,
                Status = "running"
            };

            _dbContext.ABTests.Add(test);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("A/B test created with ID: {TestId}", test.TestId);
            return test;
        }

        /// <summary>
        /// Stop an A/B test early
        /// </summary>
        public async Task StopTestAsync(Guid testId, string reason, CancellationToken ct)
        {
            var test = await _dbContext.ABTests.FindAsync(new object[] { testId }, ct);
            
            if (test == null)
            {
                throw new InvalidOperationException($"Test {testId} not found");
            }

            test.EndedAt = DateTime.UtcNow;
            test.Status = "stopped";
            test.Conclusion = $"Manually stopped: {reason}";

            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("A/B test {TestId} stopped: {Reason}", testId, reason);
        }

        /// <summary>
        /// Get test results
        /// </summary>
        public async Task<TestResults> GetTestResultsAsync(Guid testId, CancellationToken ct)
        {
            var test = await _dbContext.ABTests.FindAsync(new object[] { testId }, ct);
            
            if (test == null)
            {
                return null;
            }

            var controlTrades = await _dbContext.TradeOutcomes
                .Where(t => t.ModelVersion == test.ControlVersion && 
                    t.EntryTime >= test.StartedAt)
                .ToListAsync(ct);

            var treatmentTrades = await _dbContext.TradeOutcomes
                .Where(t => t.ModelVersion == test.TreatmentVersion && 
                    t.EntryTime >= test.StartedAt)
                .ToListAsync(ct);

            return new TestResults
            {
                TestId = testId,
                TestName = test.TestName,
                ControlMetrics = CalculateMetrics(controlTrades),
                TreatmentMetrics = CalculateMetrics(treatmentTrades),
                Duration = DateTime.UtcNow - test.StartedAt,
                IsComplete = test.Status != "running"
            };
        }

        private async Task<string> GetProductionVersionAsync(string modelType, CancellationToken ct)
        {
            var productionModel = await _dbContext.ModelPerformance
                .Where(m => m.ModelType == modelType && m.Status == "production")
                .OrderByDescending(m => m.EvaluationDate)
                .Select(m => m.ModelVersion)
                .FirstOrDefaultAsync(ct);

            return productionModel ?? "v1.0.0";
        }

        private TestMetrics CalculateMetrics(System.Collections.Generic.List<TradeOutcome> trades)
        {
            if (!trades.Any())
            {
                return new TestMetrics();
            }

            var returns = trades.Select(t => t.NetPnl ?? 0).ToList();
            var winRate = (decimal)trades.Count(t => t.IsWin) / trades.Count;
            
            return new TestMetrics
            {
                TradeCount = trades.Count,
                WinRate = winRate,
                NetPnL = returns.Sum(),
                AvgTrade = returns.Average(),
                SharpeRatio = CalculateSharpe(returns),
                ProfitFactor = CalculateProfitFactor(trades)
            };
        }

        private decimal CalculateSharpe(System.Collections.Generic.List<decimal> returns)
        {
            if (returns.Count < 10) return 0;
            var avg = returns.Average();
            var stdDev = CalculateStdDev(returns);
            return stdDev > 0 ? avg / stdDev * (decimal)Math.Sqrt(252) : 0;
        }

        private decimal CalculateProfitFactor(System.Collections.Generic.List<TradeOutcome> trades)
        {
            var grossProfit = trades.Where(t => t.NetPnl > 0).Sum(t => t.NetPnl ?? 0);
            var grossLoss = trades.Where(t => t.NetPnl < 0).Sum(t => Math.Abs(t.NetPnl ?? 0));
            return grossLoss > 0 ? grossProfit / grossLoss : grossProfit;
        }

        private decimal CalculateStdDev(System.Collections.Generic.List<decimal> values)
        {
            if (values.Count < 2) return 0;
            var avg = values.Average();
            var sumSquares = values.Sum(v => (v - avg) * (v - avg));
            return (decimal)Math.Sqrt((double)(sumSquares / values.Count));
        }
    }

    public class TestResults
    {
        public Guid TestId { get; set; }
        public string TestName { get; set; }
        public TestMetrics ControlMetrics { get; set; }
        public TestMetrics TreatmentMetrics { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsComplete { get; set; }
    }

    public class TestMetrics
    {
        public int TradeCount { get; set; }
        public decimal WinRate { get; set; }
        public decimal NetPnL { get; set; }
        public decimal AvgTrade { get; set; }
        public decimal SharpeRatio { get; set; }
        public decimal ProfitFactor { get; set; }
    }
}
