using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImprovementEngine.Models
{
    /// <summary>
    /// Aggregated performance metrics for strategies over time periods
    /// </summary>
    [Table("strategy_performance")]
    public class StrategyPerformance
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("strategy_id")]
        [MaxLength(50)]
        public string StrategyId { get; set; }

        [Required]
        [Column("model_version")]
        [MaxLength(20)]
        public string ModelVersion { get; set; }

        [Required]
        [Column("period_type")]
        [MaxLength(10)]
        public string PeriodType { get; set; } // DAY, WEEK, MONTH

        [Required]
        [Column("period_start")]
        public DateTime PeriodStart { get; set; }

        // Trade counts
        [Column("total_trades")]
        public int TotalTrades { get; set; }

        [Column("winning_trades")]
        public int WinningTrades { get; set; }

        [Column("losing_trades")]
        public int LosingTrades { get; set; }

        [Column("breakeven_trades")]
        public int BreakevenTrades { get; set; }

        // Financial metrics
        [Column("gross_profit")]
        [Precision(18, 4)]
        public decimal GrossProfit { get; set; }

        [Column("gross_loss")]
        [Precision(18, 4)]
        public decimal GrossLoss { get; set; }

        [Column("net_pnl")]
        [Precision(18, 4)]
        public decimal NetPnl { get; set; }

        [Column("total_commission")]
        [Precision(18, 4)]
        public decimal TotalCommission { get; set; }

        // Performance ratios
        [Column("win_rate")]
        [Precision(5, 4)]
        public decimal? WinRate { get; set; }

        [Column("profit_factor")]
        [Precision(8, 4)]
        public decimal? ProfitFactor { get; set; }

        [Column("avg_win")]
        [Precision(18, 4)]
        public decimal? AvgWin { get; set; }

        [Column("avg_loss")]
        [Precision(18, 4)]
        public decimal? AvgLoss { get; set; }

        [Column("avg_trade")]
        [Precision(18, 4)]
        public decimal? AvgTrade { get; set; }

        [Column("largest_win")]
        [Precision(18, 4)]
        public decimal? LargestWin { get; set; }

        [Column("largest_loss")]
        [Precision(18, 4)]
        public decimal? LargestLoss { get; set; }

        // Risk metrics
        [Column("max_drawdown")]
        [Precision(8, 4)]
        public decimal? MaxDrawdown { get; set; }

        [Column("max_drawdown_duration_days")]
        public int? MaxDrawdownDurationDays { get; set; }

        [Column("sharpe_ratio")]
        [Precision(8, 4)]
        public decimal? SharpeRatio { get; set; }

        [Column("sortino_ratio")]
        [Precision(8, 4)]
        public decimal? SortinoRatio { get; set; }

        [Column("calmar_ratio")]
        [Precision(8, 4)]
        public decimal? CalmarRatio { get; set; }

        // Execution quality
        [Column("avg_slippage")]
        [Precision(18, 8)]
        public decimal? AvgSlippage { get; set; }

        [Column("avg_fill_time_ms")]
        public int? AvgFillTimeMs { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Computed properties
        [NotMapped]
        public bool IsProfitable => NetPnl > 0;

        [NotMapped]
        public decimal? RiskRewardRatio => AvgLoss != 0 
            ? Math.Abs(AvgWin.GetValueOrDefault() / AvgLoss.GetValueOrDefault()) 
            : null;

        public void CalculateDerivedMetrics()
        {
            if (TotalTrades > 0)
            {
                WinRate = (decimal)WinningTrades / TotalTrades;
                AvgWin = WinningTrades > 0 ? GrossProfit / WinningTrades : 0;
                AvgLoss = LosingTrades > 0 ? GrossLoss / LosingTrades : 0;
                AvgTrade = NetPnl / TotalTrades;
            }

            if (GrossLoss != 0)
            {
                ProfitFactor = Math.Abs(GrossProfit / GrossLoss);
            }
        }
    }
}
