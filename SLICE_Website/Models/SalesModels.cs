using System;

namespace SLICE_Website.Models
{
    public class MenuProduct
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public decimal BasePrice { get; set; }
        public string Category { get; set; }

        // Smart POS Depletion Tracking
        public int MaxCookable { get; set; }
        public bool IsInStock => MaxCookable > 0;

        public string ImagePath { get; set; }

        // Dynamically builds the exact location on ANY computer
        public string FullImagePath
        {
            get
            {
                if (string.IsNullOrEmpty(ImagePath)) return null;
                return System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "Menu", ImagePath);
            }
        }

        // Helper for the UI to hide/show the image container
        public bool HasImage => !string.IsNullOrEmpty(ImagePath);
    }

    public class CartItem
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public int Qty { get; set; }
        public decimal TotalPrice => Price * Qty;
    }

    public class SaleRecord
    {
        public int SaleID { get; set; }
        public string ProductName { get; set; }
        public int QuantitySold { get; set; }
        public decimal TotalAmount { get; set; }
        public string ReferenceNumber { get; set; }
        public string PaymentMethod { get; set; }

        // The raw time from the Azure SQL Database (UTC)
        public DateTime TransactionDate { get; set; }

        // Automatically converts Azure UTC to Philippine Time (UTC+8)
        public DateTime LocalTransactionDate => TransactionDate.AddHours(8);

        public string TransactionStatus { get; set; }
    }
}