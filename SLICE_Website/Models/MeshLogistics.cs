using System;

namespace SLICE_Website.Models
{
    public class MeshLogistics
    {
        // --- DATABASE COLUMNS ---
        public int TransferID { get; set; }
        public int FromBranchID { get; set; }
        public int ToBranchID { get; set; }
        public string Status { get; set; } = "Pending";

        // Nullable because:
        // 1. In a "Request" (Pull), the SenderID (Manager at Source) is null until Approved.
        // 2. In a "Shipment" (Push), the ReceiverID (Staff at Dest) is null until Received.
        public int? SenderID { get; set; }
        public int? ReceiverID { get; set; }

        public DateTime? SentDate { get; set; }
        public DateTime? ReceivedDate { get; set; }

        // --- DISPLAY / VIEW PROPERTIES (Not in Table, Populated by SQL Joins) ---

        // Initialized to string.Empty to prevent null reference warnings
        public string FromBranchName { get; set; } = string.Empty;
        public string ToBranchName { get; set; } = string.Empty;

        // Sum of all items in the transfer
        public decimal TotalQuantity { get; set; }
    }
}