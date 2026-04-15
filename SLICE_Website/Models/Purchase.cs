using System;
using System.Collections.Generic;

namespace SLICE_Website.Models
{
    public class Purchase
    {
        public int PurchaseID { get; set; }
        public string Supplier { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime PurchaseDate { get; set; }
        public int PurchasedBy { get; set; }
        public int BranchID { get; set; }

        // Helper property for UI binding
        public List<PurchaseDetail> Details { get; set; } = new List<PurchaseDetail>();
    }
}