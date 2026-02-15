using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace ImprovementEngine.Models
{
    /// <summary>
    /// Stores every model prediction for audit trail and accuracy analysis
    /// </summary>
    [Table("model_predictions")]
    public class ModelPrediction
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("prediction_id")]
        public Guid PredictionId { get; set; }

        [Required]
        [Column("model_version")]
        [MaxLength(20)]
        public string ModelVersion { get; set; }

        [Required]
        [Column("model_type")]
        [MaxLength(30)]
        public string ModelType { get; set; } // direction, volatility, regime

        [Required]
        [Column("symbol")]
        [MaxLength(10)]
        public string Symbol { get; set; }

        [Required]
        [Column("predicted_at")]
        public DateTime PredictedAt { get; set; }

        [Required]
        [Column("prediction_horizon_minutes")]
        public int PredictionHorizonMinutes { get; set; }

        [Column("predicted_direction")]
        [MaxLength(4)]
        public string PredictedDirection { get; set; }

        [Column("confidence_score")]
        [Precision(5, 4)]
        public decimal? ConfidenceScore { get; set; }

        [Column("predicted_volatility")]
        [Precision(10, 6)]
        public decimal? PredictedVolatility { get; set; }

        [Required]
        [Column("feature_vector", TypeName = "jsonb")]
        public JsonDocument FeatureVector { get; set; }

        [Column("feature_hash")]
        [MaxLength(64)]
        public string FeatureHash { get; set; }

        // Validation fields (filled later)
        [Column("actual_direction")]
        [MaxLength(4)]
        public string ActualDirection { get; set; }

        [Column("actual_return")]
        [Precision(18, 8)]
        public decimal? ActualReturn { get; set; }

        [Column("prediction_accuracy")]
        public bool? PredictionAccuracy { get; set; }

        [Column("validated_at")]
        public DateTime? ValidatedAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Helper methods
        public bool NeedsValidation() => !ValidatedAt.HasValue;

        public void Validate(string actualDirection, decimal actualReturn)
        {
            ActualDirection = actualDirection;
            ActualReturn = actualReturn;
            PredictionAccuracy = PredictedDirection == actualDirection;
            ValidatedAt = DateTime.UtcNow;
        }
    }
}
