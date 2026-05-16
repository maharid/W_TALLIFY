using System;
using System.Collections.Generic;

namespace ProjectTallify.Models
{
    public class Contestant
    {
        public int Id { get; set; }

        // FK back to the Event
        public int EventId { get; set; }
        public Event Event { get; set; } = null!;

        // C001, C002, etc
        public string Code { get; set; } = null!;

        // Full name
        public string Name { get; set; } = null!;

        // School / Department / Org
        public string? Organization { get; set; }

        // File path or URL of their uploaded photo (optional for now)
        public string? PhotoPath { get; set; }
        public bool IsActive { get; set; } = true; // New property: determines if contestant is active in current round/event

        public ICollection<Score> Scores { get; set; } = new List<Score>();
        public ICollection<ComputedRoundScore> ComputedRoundScores { get; set; } = new List<ComputedRoundScore>();
        public ICollection<OverallScore> OverallScores { get; set; } = new List<OverallScore>();
    }
}
