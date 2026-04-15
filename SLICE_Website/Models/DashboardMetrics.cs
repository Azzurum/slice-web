using System;
using System.Collections.Generic;

namespace SLICE_Website.Models
{
    public class DashboardMetrics
    {
        // --- EXISTING PROPERTIES ---
        public decimal TotalSalesValue { get; set; }
        public int TotalSalesCount { get; set; }
        public int LowStockCount { get; set; }
        public int PendingShipments { get; set; }

        // --- NEW PROPERTIES FOR FINANCE/P&L ---
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetProfit => TotalRevenue - TotalExpenses;

        // --- PHASE 3: WASTE TRACKING ---
        public decimal TotalWasteCost { get; set; }

        // --- Chart Data Collections ---
        public List<BranchPerformance> BranchRanking { get; set; } = new List<BranchPerformance>();
        public List<InventoryHealth> BranchStockHealth { get; set; } = new List<InventoryHealth>();
        public List<ProductMix> TopProducts { get; set; } = new List<ProductMix>();

        // --- Critical Alerts List ---
        public List<LowStockAlert> Alerts { get; set; } = new List<LowStockAlert>();

        // --- Recent Activity Feed ---
        public List<RecentTransaction> RecentActivity { get; set; } = new List<RecentTransaction>();
    }

    public class BranchPerformance
    {
        public string BranchName { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpense { get; set; }
    }

    public class InventoryHealth
    {
        public string BranchName { get; set; }
        public int GoodStock { get; set; }
        public int LowStock { get; set; }
        public int CriticalStock { get; set; }
    }

    public class ProductMix
    {
        public string ProductName { get; set; }
        public int QuantitySold { get; set; }
    }

    public class LowStockAlert
    {
        public string BranchName { get; set; }
        public string ItemName { get; set; }
        public decimal CurrentQty { get; set; }
        public decimal Threshold { get; set; }
        public bool IsCritical => CurrentQty <= 0;
        public string StatusColor => IsCritical ? "#C0392B" : "#E67E22";
    }

    public class RecentTransaction
    {
        public int SaleID { get; set; }
        public string BranchName { get; set; }
        public string ProductName { get; set; }
        public int QuantitySold { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime TransactionDate { get; set; }

        public string TimeAgo
        {
            get
            {
                var diff = DateTime.UtcNow - TransactionDate;
                if (diff.TotalMinutes < -10) diff = DateTime.Now - TransactionDate;

                if (diff.TotalSeconds < 60) return "Just now";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
                return $"{(int)diff.TotalDays}d ago";
            }
        }
    }
}