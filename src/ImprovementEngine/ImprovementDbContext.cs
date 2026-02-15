using ImprovementEngine.Models;
using Microsoft.EntityFrameworkCore;

namespace ImprovementEngine
{
    /// <summary>
    /// Database context for the Improvement Engine
    /// </summary>
    public class ImprovementDbContext : DbContext
    {
        public ImprovementDbContext(DbContextOptions<ImprovementDbContext> options) : base(options)
        {
        }

        public DbSet<TradeOutcome> TradeOutcomes { get; set; }
        public DbSet<ModelPrediction> ModelPredictions { get; set; }
        public DbSet<MarketConditions> MarketConditions { get; set; }
        public DbSet<StrategyPerformance> StrategyPerformances { get; set; }
        public DbSet<ModelPerformance> ModelPerformances { get; set; }
        public DbSet<ImprovementEvent> ImprovementEvents { get; set; }
        public DbSet<ABTest> ABTests { get; set; }
        public DbSet<OptimizationQueue> OptimizationQueue { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure TradeOutcome indexes
            modelBuilder.Entity<TradeOutcome>()
                .HasIndex(t => new { t.StrategyId, t.EntryTime });
            modelBuilder.Entity<TradeOutcome>()
                .HasIndex(t => new { t.ModelVersion, t.EntryTime });

            // Configure ModelPrediction indexes
            modelBuilder.Entity<ModelPrediction>()
                .HasIndex(p => new { p.ModelVersion, p.PredictedAt });
            modelBuilder.Entity<ModelPrediction>()
                .HasIndex(p => p.ValidatedAt)
                .HasFilter("validated_at IS NULL");

            // Configure MarketConditions as TimescaleDB hypertable
            modelBuilder.Entity<MarketConditions>()
                .HasIndex(m => new { m.Symbol, m.Timestamp });

            // Configure StrategyPerformance
            modelBuilder.Entity<StrategyPerformance>()
                .HasIndex(s => new { s.StrategyId, s.PeriodStart });

            // Configure ModelPerformance
            modelBuilder.Entity<ModelPerformance>()
                .HasIndex(m => new { m.ModelVersion, m.EvaluationDate });

            // Configure ImprovementEvents
            modelBuilder.Entity<ImprovementEvent>()
                .HasIndex(e => new { e.EventType, e.EventTimestamp });

            // Configure ABTests
            modelBuilder.Entity<ABTest>()
                .HasIndex(t => t.Status);

            // Configure OptimizationQueue
            modelBuilder.Entity<OptimizationQueue>()
                .HasIndex(t => new { t.NextRunAt, t.Enabled, t.Status })
                .HasFilter("enabled = true AND status = 'pending'");
        }
    }
}
