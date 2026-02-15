using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace ImprovementEngine.Models
{
    /// <summary>
    /// Audit log of all system improvements and changes
    /// </summary>
    [Table("improvement_events")]
    public class ImprovementEvent
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("event_type")]
        [MaxLength(30)]
        public string EventType { get; set; }
        // model_deployed, params_optimized, strategy_disabled, 
        // feature_added, risk_adjusted, ab_test_started

        [Column("event_timestamp")]
        public DateTime EventTimestamp { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("component_type")]
        [MaxLength(30)]
        public string ComponentType { get; set; }
        // model, strategy, risk_params, feature, threshold

        [Required]
        [Column("component_id")]
        [MaxLength(50)]
        public string ComponentId { get; set; }

        [Column("old_value", TypeName = "jsonb")]
        public JsonDocument OldValue { get; set; }

        [Column("new_value", TypeName = "jsonb")]
        public JsonDocument NewValue { get; set; }

        [Column("trigger_reason")]
        [MaxLength(100)]
        public string TriggerReason { get; set; }
        // performance_degradation, scheduled_retrain, manual_override,
        // threshold_breach, drift_detected, optimization_complete

        [Column("performance_before", TypeName = "jsonb")]
        public JsonDocument PerformanceBefore { get; set; }

        [Column("performance_after", TypeName = "jsonb")]
        public JsonDocument PerformanceAfter { get; set; }

        [Column("improvement_metrics", TypeName = "jsonb")]
        public JsonDocument ImprovementMetrics { get; set; }

        [Column("automated")]
        public bool Automated { get; set; } = true;

        [Column("approved_by")]
        [MaxLength(50)]
        public string ApprovedBy { get; set; }

        [Column("approval_timestamp")]
        public DateTime? ApprovalTimestamp { get; set; }

        [Column("can_rollback")]
        public bool CanRollback { get; set; } = true;

        [Column("rollback_timestamp")]
        public DateTime? RollbackTimestamp { get; set; }

        // Discord notification tracking
        [Column("discord_notified")]
        public bool DiscordNotified { get; set; } = false;

        [Column("notification_message_id")]
        [MaxLength(100)]
        public string NotificationMessageId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Helper methods
        public bool RequiresApproval() => !Automated;

        public bool CanBeRolledBack() => CanRollback && !RollbackTimestamp.HasValue;

        public TimeSpan? TimeSinceEvent() => DateTime.UtcNow - EventTimestamp;
    }
}
