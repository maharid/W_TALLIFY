using System;

namespace ProjectTallify.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        public int? EventId { get; set; }
        public Event? Event { get; set; }

        public int? UserId { get; set; }
        public User? User { get; set; }

        // Snapshot of who performed the action (e.g. "John Doe", "Judge #1")
        public string? UserName { get; set; }

        // Role at the time of action (e.g. "Organizer", "Judge", "Scorer")
        public string? UserRole { get; set; }

        public string Action { get; set; } = null!;    // e.g. "Created event", "Updated criteria"
        
        // Extra metadata (e.g. "Round 1", "Contestant C-001", etc.)
        public string? Details { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
