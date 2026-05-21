using System;

namespace ProjectTallify.Models
{
    public class NotificationLog
    {
        public int Id { get; set; }

        // Link to the recipient organizer
        public int? OrganizerId { get; set; }
        public Organizer? Organizer { get; set; }

        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string Type { get; set; } = "info"; // info, success, warning, error
        public string? ActionUrl { get; set; }
        
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
