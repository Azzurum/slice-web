namespace SLICE_Website.Models
{
    public class PurchaseDetail
    {
        public int DetailID { get; set; }
        public int PurchaseID { get; set; }
        public int ItemID { get; set; }
        public string ItemName { get; set; } // Fetched via SQL Join
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal => Quantity * UnitPrice;
    }
}