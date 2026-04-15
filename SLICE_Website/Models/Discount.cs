using System;

namespace SLICE_Website.Models
{
    public class Discount
    {
        public int DiscountID { get; set; }
        public string DiscountName { get; set; }
        public string DiscountType { get; set; }
        public string Scope { get; set; }
        public string ValueType { get; set; }
        public decimal DiscountValue { get; set; }

        public bool IsActive { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public string RequiredRole { get; set; }
        public bool IsStackable { get; set; }

        // Runtime properties (Not stored in the main DB table)
        public string ReferenceID { get; set; }
        public string Reason { get; set; }
        public decimal CalculatedAmount { get; set; }
    }
}