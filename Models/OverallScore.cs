using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectTallify.Models
{
    public class OverallScore
    {
        [Key]
        public int Id { get; set; }

        public int EventId { get; set; }
        public Event Event { get; set; } = null!;

        public int ContestantId { get; set; }
        public Contestant Contestant { get; set; } = null!;

        public decimal Score { get; set; } // Overall total score
        public decimal Rank { get; set; } // Overall rank

        public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
    }
}
