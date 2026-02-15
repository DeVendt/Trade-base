using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImprovementEngine.Models
{
    /// <summary>
    /// Represents a completed trade with full outcome data for analysis
    /// </summary>
    [Table("trade_outcomes")]
    public class TradeOutcome
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("trade_id")]
        public Guid TradeId { get; set; }

        [Required]
        [Column("strategy_id")]
        [MaxLength(50)]
        public string StrategyId { get; set; }

        [Required]
        [Column("model_version")]
        [MaxLength(20)]
        public string ModelVersion { get; set; }

        [Required]
        [Column("symbol")]
        [MaxLength(10)]
        public string Symbol { get; set; }

        [Required]
        [Column("direction")]
        [MaxLength(4)]
        public string Direction { get; set; } // LONG or SHORT

        [Required]
        [Column("entry_time")]
        public DateTime EntryTime { get; set; }

        [Column("exit_time")]
        public DateTime? ExitTime { get; set; }

        [Required]
        [Column("entry_price")]
        [Precision(18, 8)]
        public decimal EntryPrice { get; set; }

        [Column("exit_price")]
        [Precision(18, 8)]
        public decimal? ExitPrice { get; set; }

        [Required]
        [Column("quantity")]
        public int Quantity { get; set; }

        [Column("gross_pnl")]
        [Precision(18, 4)]
        public decimal? GrossPnl { get; set; }

        [Column("net_pnl")]
        [Precision(18, 4)]
        public decimal? NetPnl { get; set; }

        [Column("commission")]
        [Precision(18, 4)]
        public decimal Commission { get; set; } = 0;

        [Column("slippage")]
        [Precision(18, 8)]
        public decimal Slippage { get; set; } = 0;

        [Column("duration_seconds")]
        public int? DurationSeconds { get; set; }

        [Column("max_favorable_excursion")]
        [Precision(18, 4)]
        public decimal? MaxFavorableExcursion { get; set; }

        [Column("max_adverse_excursion")]
        [Precision(18, 4)]
        public decimal? MaxAdverseExcursion { get; set; }

        [Column("outcome_type")]
        [MaxLength(20)]
        public string OutcomeType { get; set; } // WIN, LOSS, BREAKEVEN, OPEN

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Computed properties
        [NotMapped]
        public bool IsWin => NetPnl > 0;

        [NotMapped]
        public bool IsComplete => ExitTime.HasValue && NetPnl.HasValue;

        [NotMapped]
        public decimal? ReturnPercent => EntryPrice > 0 
            ? (NetPnl / (EntryPrice * Quantity)) * 100 
            : null;
    }
}
