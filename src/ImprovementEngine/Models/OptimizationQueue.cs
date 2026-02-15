using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace ImprovementEngine.Models
{
    /// <summary>
    /// Queue for recurring continuous optimization tasks
    /// </summary>
    [Table("optimization_queue")]
    public class OptimizationQueue
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("task_type")]
        [MaxLength(30)]
        public string TaskType { get; set; }
        // hyperparameter, feature_selection, strategy_weights,
        // model_retrain, threshold_tuning, regime_detection

        [Required]
        [Column("component_id")]
        [MaxLength(50)]
        public string ComponentId { get; set; }

        [Required]
        [Column("frequency")]
        [MaxLength(20)]
        public string Frequency { get; set; }
        // hourly, 4hourly, daily, weekly, biweekly, monthly

        [Column("last_run_at")]
        public DateTime? LastRunAt { get; set; }

        [Required]
        [Column("next_run_at")]
        public DateTime NextRunAt { get; set; }

        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = "pending";
        // pending, running, completed, failed, paused

        [Column("priority")]
        public int Priority { get; set; } = 5; // 1 = highest, 10 = lowest

        [Column("last_result", TypeName = "jsonb")]
        public JsonDocument LastResult { get; set; }

        [Column("last_error")]
        public string LastError { get; set; }

        [Column("consecutive_failures")]
        public int ConsecutiveFailures { get; set; }

        [Required]
        [Column("config", TypeName = "jsonb")]
        public JsonDocument Config { get; set; }

        [Column("enabled")]
        public bool Enabled { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Helper methods
        public bool IsOverdue() => NextRunAt < DateTime.UtcNow && Status == "pending";

        public bool ShouldRun() => Enabled && Status == "pending" && NextRunAt <= DateTime.UtcNow;

        public bool HasFailedRepeatedly(int threshold = 3) => ConsecutiveFailures >= threshold;

        public void MarkCompleted(JsonDocument result)
        {
            Status = "completed";
            LastRunAt = DateTime.UtcNow;
            LastResult = result;
            ConsecutiveFailures = 0;
            ScheduleNextRun();
        }

        public void MarkFailed(string error)
        {
            Status = "failed";
            LastRunAt = DateTime.UtcNow;
            LastError = error;
            ConsecutiveFailures++;
            ScheduleNextRun();
        }

        public void ScheduleNextRun()
        {
            NextRunAt = Frequency.ToLower() switch
            {
                "hourly" => DateTime.UtcNow.AddHours(1),
                "4hourly" => DateTime.UtcNow.AddHours(4),
                "daily" => DateTime.UtcNow.AddDays(1),
                "weekly" => DateTime.UtcNow.AddDays(7),
                "biweekly" => DateTime.UtcNow.AddDays(14),
                "monthly" => DateTime.UtcNow.AddMonths(1),
                _ => DateTime.UtcNow.AddDays(1)
            };
            Status = "pending";
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
