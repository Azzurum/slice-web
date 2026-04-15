using System;

namespace SLICE_Website.Models
{
    public class FinancialLedger
    {
        public int LedgerID { get; set; }
        public DateTime TransactionDate { get; set; }
        public int? BranchID { get; set; } // Null for Global HQ Expenses
        public string Type { get; set; } // "Income" or "Expense"
        public string Category { get; set; } // "Sales", "Ingredients", "Waste", etc.
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public int? ReferenceID { get; set; } // SaleID or PurchaseID
    }
}