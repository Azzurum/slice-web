using System;

namespace SLICE_Website.Models
{
    public class AuditEntry
    {
        public DateTime Timestamp { get; set; }

        // Automatically converts Azure UTC to Philippine Time (UTC+8)
        public DateTime LocalTimestamp => Timestamp.AddHours(8);

        public string ActivityType { get; set; } // e.g., "SALE", "WASTE"
        public string Description { get; set; }
        public string BranchName { get; set; }
        public string PerformedBy { get; set; }

        public string ReferenceNumber { get; set; }

        // --- UI Helper for XAML Binding ---
        // This calculates the color based on the ActivityType
        public string BadgeColor
        {
            get
            {
                switch (ActivityType?.ToUpper())
                {
                    case "SALE":
                    case "SHIPMENT":
                        return "#27AE60"; // Green

                    case "Z-READING":
                        return "#8E44AD"; // Purple

                    case "SECURITY":
                        return "#F39C12"; // Golden Orange (For Password Resets)

                    case "WASTE":
                    case "DELETE":
                        return "#C0392B"; // Red

                    case "LOGIN":
                    case "UPDATE":
                        return "#2980B9"; // Blue

                    default:
                        return "#95A5A6"; // Gray
                }
            }
        }

        // Helper to match the XAML binding "ActionType" if the XAML uses that name
        public string ActionType => ActivityType;
    }
}