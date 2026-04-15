using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using SLICE_Website.Models;

namespace SLICE_Website.Data
{
    public class ProcurementRepository
    {
        private readonly DatabaseService _dbService;

        public ProcurementRepository(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        public void ProcessPurchase(Purchase header, List<PurchaseDetail> details)
        {
            using (var conn = _dbService.GetConnection())
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Insert Purchase Header (Financial Record)
                        string sqlHeader = @"
                                INSERT INTO Purchases (Supplier, TotalAmount, PurchasedBy, BranchID, PurchaseDate)
                                VALUES (@Supplier, @TotalAmount, @PurchasedBy, @BranchID, GETDATE());
                                SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int newPurchaseId = conn.ExecuteScalar<int>(sqlHeader, header, trans);

                        // 2. Create Logistics "In-Transit" Record (Delivery from Supplier -> HQ)
                        // Used a dummy FromBranchID (e.g., 0 or NULL) to indicate an external supplier
                        string sqlLogistics = @"
                            INSERT INTO MeshLogistics (FromBranchID, ToBranchID, Status, SenderID, SentDate)
                            VALUES (NULL, @BranchID, 'In-Transit', @PurchasedBy, GETDATE());
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int newTransferId = conn.ExecuteScalar<int>(sqlLogistics, new { BranchID = header.BranchID, PurchasedBy = header.PurchasedBy }, trans);

                        // 3. Process Details (Convert to Base Units and attach to both Purchase and Logistics)
                        string sqlConversion = "SELECT ISNULL(ConversionRatio, 1) FROM MasterInventory WHERE ItemID = @ItemId";

                        string sqlPurchaseDetail = @"
                            INSERT INTO PurchaseDetails (PurchaseID, ItemID, Quantity, UnitPrice)
                            VALUES (@Pid, @ItemId, @Qty, @Price)";

                        string sqlWaybillDetail = @"
                            INSERT INTO WaybillDetails (TransferID, ItemID, Quantity)
                            VALUES (@TransferID, @ItemID, @Quantity)";

                        foreach (var item in details)
                        {
                            decimal ratio = conn.ExecuteScalar<decimal>(sqlConversion, new { ItemId = item.ItemID }, trans);
                            if (ratio <= 0) ratio = 1;

                            decimal baseQty = item.Quantity * ratio;
                            decimal baseUnitPrice = item.UnitPrice / ratio;

                            // A. Save the Financial Purchase Detail
                            conn.Execute(sqlPurchaseDetail, new { Pid = newPurchaseId, ItemId = item.ItemID, Qty = baseQty, Price = baseUnitPrice }, trans);

                            // B. Save the Logistics Waybill Detail (This puts it in the Incoming Deliveries screen!)
                            conn.Execute(sqlWaybillDetail, new { TransferID = newTransferId, ItemID = item.ItemID, Quantity = baseQty }, trans);
                        }

                        // 4. LOG TO FINANCIAL LEDGER
                        string sqlLedger = @"
                            INSERT INTO FinancialLedger (TransactionDate, BranchID, Type, Category, Amount, Description, ReferenceID)
                            VALUES (GETDATE(), @BranchID, 'Expense', 'Ingredients', @Amount, @Desc, @RefID)";

                        conn.Execute(sqlLedger, new
                        {
                            BranchID = header.BranchID,
                            Amount = header.TotalAmount,
                            Desc = $"Purchase from {header.Supplier}",
                            RefID = newPurchaseId
                        }, trans);

                        trans.Commit();
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}