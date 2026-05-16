using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectTallify.Models
{
    public class Score
    {
        [Key]
        public int Id { get; set; }

        public int EventId { get; set; }
        public Event Event { get; set; } = null!;

        public int RoundId { get; set; }
        public Round Round { get; set; } = null!;

        public int ContestantId { get; set; }
        public Contestant Contestant { get; set; } = null!;

        // Nullable for ORW scorers who might not be linked to a Judge entry
        public int? JudgeId { get; set; }
        public Judge? Judge { get; set; }

        public int CriteriaId { get; set; }
        public Criteria Criteria { get; set; } = null!;

        public decimal Value { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
