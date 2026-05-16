using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectTallify.Models
{
    public class ComputedRoundScore
    {
        [Key]
        public int Id { get; set; }

        public int EventId { get; set; }
        public Event Event { get; set; } = null!;

        public int RoundId { get; set; }
        public Round Round { get; set; } = null!;

        public int ContestantId { get; set; }
        public Contestant Contestant { get; set; } = null!;

        public int? CriteriaId { get; set; } // Nullable if storing round total, not per-criterion
        public Criteria? Criteria { get; set; }

        public decimal Score { get; set; } // Computed score for criterion/round
        public decimal Rank { get; set; } // Rank within the round

        public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
    }
}
