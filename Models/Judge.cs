using System;

namespace ProjectTallify.Models
{
    public class Judge
    {
        public int Id { get; set; }

        public int EventId { get; set; }
        public Event Event { get; set; } = null!;

        public string Name { get; set; } = null!;
        public string? Email { get; set; }

        // PIN they will use to login to scoring
        public string Pin { get; set; } = null!;
        public bool IsActive { get; set; } = true;

        // Verification Logic
        public string? VerificationToken { get; set; }
        public bool IsInviteSent { get; set; } = false;
        public bool IsEmailVerified { get; set; } = false;
        public bool IsAccessSent { get; set; } = false;
        public DateTime? DateVerified { get; set; }

        public ICollection<Score> Scores { get; set; } = new List<Score>();
    }
}
