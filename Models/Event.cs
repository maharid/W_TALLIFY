using System;
using System.ComponentModel.DataAnnotations.Schema;   // <- add this

namespace ProjectTallify.Models
{
    public class Event
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;
        public string Venue { get; set; } = null!;
        public string? Description { get; set; }

        public string ScoringLogic { get; set; } = "WA";

        public DateTime Schedule { get; set; }


        public string Status { get; set; } = "preparing";

        public string AccessCode { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int OrganizerId { get; set; }
        public Organizer Organizer { get; set; } = null!;

        public string? ContestantsJson { get; set; }
        public string? AccessJson { get; set; }
        public string? RoundsJson { get; set; }

        // Branding / Appearance
        public string? ThemeColor { get; set; } // e.g. "#ff007a"
        public string? HeaderImage { get; set; } // path or filename
        public bool IsArchived { get; set; } = false;

        public ICollection<Contestant> Contestants { get; set; } = new List<Contestant>();
        public ICollection<Judge> Judges { get; set; } = new List<Judge>();
        public ICollection<Round> Rounds { get; set; } = new List<Round>();
        public ICollection<Score> Scores { get; set; } = new List<Score>();
        public ICollection<ComputedRoundScore> ComputedRoundScores { get; set; } = new List<ComputedRoundScore>();
        public ICollection<OverallScore> OverallScores { get; set; } = new List<OverallScore>();
    }
}
