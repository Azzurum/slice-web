using System;

namespace SLICE_Website.Models
{
    public class User
    {
        public int UserID { get; set; }
        public string? Username { get; set; }
        public string? PasswordHash { get; set; }
        public string? FullName { get; set; }
        public string? Role { get; set; }
        public int? BranchID { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Email { get; set; }
        public string? ResetCode { get; set; }
        public DateTime? ResetCodeExpiry { get; set; }

        // --- JOINED DATA ---
        public string? BranchName { get; set; }

        // --- UI HELPERS (For XAML Binding) ---

        // Generates Initials
        public string Initials => string.IsNullOrEmpty(FullName) ? "?" : FullName.Substring(0, 1).ToUpper();

        // Badge Color Logic
        public string RoleColor
        {
            get
            {
                switch (Role)
                {
                    case "Super-Admin": return "#C0392B"; // Red
                    case "Manager": return "#2980B9";     // Blue
                    default: return "#7F8C8D";            // Gray
                }
            }
        }

        public string StatusColor => IsActive ? "#27AE60" : "#95A5A6"; // Green vs Gray
        public string StatusText => IsActive ? "Active" : "Inactive";
    }
}