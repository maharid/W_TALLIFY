namespace ProjectTallify.Models
{
    public class User
    {
        public int Id { get; set; }

        // Names
        public string FirstName { get; set; } = "";
        public string? LastName { get; set; }

        // Login
        public string Email { get; set; } = "";
        public string HashedPassword { get; set; } = "";

        // Role: Organizer, Admin, etc.
        public string Role { get; set; } = "Organizer";

        // Email confirmation
        public bool EmailConfirmed { get; set; } = false;
        public string? EmailVerificationToken { get; set; }
        public DateTime? EmailVerificationTokenExpiresAt { get; set; }

        // Password reset
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpiresAt { get; set; }

        // Remember me
        public string? RememberMeToken { get; set; }
        public DateTime? RememberMeTokenExpiresAt { get; set; }

        // Active flag
        public bool IsActive { get; set; } = true;

        // Global Theme (Admin/Organizer Dashboard)
        public string ThemeColor { get; set; } = "#ff007a";

        // Organization Settings
        public string OrganizationName { get; set; } = "Your Organization";
        public string OrganizationSubtitle { get; set; } = "Subtitle";
        public string? OrganizationPhotoPath { get; set; }
        public string? ProfilePhotoPath { get; set; }

        // Notification Preferences
        public bool EnableNotifications { get; set; } = true;
    }
}
