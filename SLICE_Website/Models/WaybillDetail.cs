namespace SLICE_Website.Models
{
    public class WaybillDetail
    {
        public int DetailID { get; set; }
        public int TransferID { get; set; }
        public int ItemID { get; set; }
        public decimal Quantity { get; set; }
        public string? ItemName { get; set; }
    }
}