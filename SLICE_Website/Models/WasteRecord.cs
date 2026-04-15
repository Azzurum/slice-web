using System;

namespace SLICE_Website.Models
{
    public class WasteRecord
    {
        public int WasteID { get; set; }
        public int BranchID { get; set; }
        public int ItemID { get; set; }
        public decimal QtyWasted { get; set; }
        public string? Reason { get; set; }
        public int RecordedBy { get; set; }
        public DateTime DateRecorded { get; set; }

        // Display Helpers
        public string? ItemName { get; set; }
        public string? RecordedByName { get; set; }
    }
}