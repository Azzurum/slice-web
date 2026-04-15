namespace SLICE_Website.Models
{
    // Represents a global ingredient or raw material definition in the Master Inventory.
    // This registry is shared across all branches to ensure consistent naming and measurement.
    public class MasterInventory
    {
        // Unique identifier for the ingredient (Primary Key).
        public int ItemID { get; set; }

        // A fake, perfectly sequential number just for the UI(1, 2, 3...)
        public int DisplayNumber { get; set; }

        // The standard name of the ingredient (e.g., "High Gluten Flour").
        public string? ItemName { get; set; }

        // The grouping category (e.g., "Dough & Flour", "Cheese & Dairy").
        public string? Category { get; set; }

        // The unit used for purchasing or shipping (e.g., "Sack", "Box", "Gallon").
        public string? BulkUnit { get; set; }

        // The smallest unit used for recipe calculation (e.g., "g", "ml", "pcs").
        public string? BaseUnit { get; set; }

        // The multiplier to convert 1 Bulk Unit into Base Units.
        // Example: If 1 Sack = 25,000 grams, this value is 25000.
        public decimal ConversionRatio { get; set; }

        public decimal TotalStock { get; set; }

        // This stores ONLY the filename (e.g., "flour.png") in the database
        public string ImagePath { get; set; }

        // ---------------------------------------------------------
        // HELPER PROPERTIES (Read-Only)
        // ---------------------------------------------------------

        // Dynamically rebuilds the path for ingredients based on the computer running the app
        public string FullImagePath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ImagePath)) return null;
                // FIXED: Changed "Inventory" to "Ingredients" to match the actual folder structure
                return System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "Ingredients", ImagePath);
            }
        }

        // Returns a formatted string description of the item and its conversion logic.
        // Handles null values gracefully to prevent UI crashes.
        public string FullDescription =>
            $"{ItemName ?? "Unknown Item"} ({ConversionRatio:N0} {BaseUnit ?? "-"} per {BulkUnit ?? "-"})";
    }
}