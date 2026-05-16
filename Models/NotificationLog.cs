using System;

namespace ProjectTallify.Models
{
    public class NotificationLog
    {
        public int Id { get; set; }

        // Link to the recipient user
        public int? UserId { get; set; }
        public User? User { get; set; }

        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string Type { get; set; } = "info"; // info, success, warning, error
        
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
