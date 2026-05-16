using System;
using System.Collections.Generic;

namespace ProjectTallify.Models
{
    public class Round
    {
        public int Id { get; set; }

        public int EventId { get; set; }
        public Event Event { get; set; } = null!;

        public string Name { get; set; } = null!;
        public int Order { get; set; }

        // "criteria" or "orw" or other types in the future
        public string RoundType { get; set; } = null!;
        public bool IsActive { get; set; } = false;
        public string Status { get; set; } = "pending"; // pending, ongoing, finished

        public ICollection<Criteria> Criterias { get; set; } = new List<Criteria>();
        public ICollection<Score> Scores { get; set; } = new List<Score>();
        public ICollection<ComputedRoundScore> ComputedRoundScores { get; set; } = new List<ComputedRoundScore>();
    }
}
