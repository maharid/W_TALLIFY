using System;

namespace ProjectTallify.Models
{
    public class Criteria
    {
        public int Id { get; set; }

        // FK back to Event
        public int EventId { get; set; }
        public Event Event { get; set; } = null!;

        // FK back to Round
        public int? RoundId { get; set; }
        public Round? Round { get; set; }

        // e.g. "Beauty & Poise", "Talent", "Q&A"
        public string Name { get; set; } = null!;

        // e.g. 30, 40, etc. (percent)
        public decimal WeightPercent { get; set; }

        // Max points a judge can give for this criteria
        public decimal MaxPoints { get; set; }

        // Min points a judge can give for this criteria
        public decimal MinPoints { get; set; }

        // NEW: Explicit flag for derived criteria
        public bool IsDerived { get; set; } = false;

        // FK to the source round (if derived)
        public int? DerivedFromRoundId { get; set; }
        public Round? DerivedFromRound { get; set; }

        // Optional ordering inside a round
        public int? DisplayOrder { get; set; }

        public ICollection<Score> Scores { get; set; } = new List<Score>();
        public ICollection<ComputedRoundScore> ComputedRoundScores { get; set; } = new List<ComputedRoundScore>();
    }
}
