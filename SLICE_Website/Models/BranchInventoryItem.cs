using System;

namespace SLICE_Website.Models
{
    public class BranchInventoryItem
    {
        public int StockID { get; set; }
        public int BranchID { get; set; }
        public int ItemID { get; set; }

        // Use 'string.Empty' to fix the CS8618 warning
        public string ItemName { get; set; } = string.Empty;

        public decimal CurrentQuantity { get; set; }

        // The limit where alerts trigger (e.g., 5kg)
        public decimal LowStockThreshold { get; set; }

        public DateTime? ExpirationDate { get; set; }

        // Use 'string.Empty' here as well
        public string BaseUnit { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        // The image of the ingredient
        public string ImagePath { get; set; }

        // --- HELPER PROPERTIES ---

        // Returns TRUE if we are in the danger zone
        public bool IsLowStock => CurrentQuantity <= LowStockThreshold;

        // Returns a text status for the dashboard
        public string StockStatus => IsLowStock ? "⚠️ CRITICAL LOW" : "Good";

        // Helper alias so UI can bind to "Unit" easily
        public string Unit => BaseUnit;
    }
}