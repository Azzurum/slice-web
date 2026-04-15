using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using SLICE_Website.Models;

namespace SLICE_Website.Data
{
    public class WasteRepository
    {
        private readonly DatabaseService _dbService;

        public WasteRepository(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        // 1. RECORD WASTE (With Financial Valuation)
        public void RecordWaste(int branchId, int itemId, decimal quantity, string reason, int userId)
        {
            using (var conn = _dbService.GetConnection())
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // A. Deduct from Physical Inventory
                        string sqlDeduct = @"
                            UPDATE BranchInventory 
                            SET CurrentQuantity = CurrentQuantity - @Qty, LastUpdated = GETDATE()
                            WHERE BranchID = @BranchID AND ItemID = @ItemID";

                        conn.Execute(sqlDeduct, new { Qty = quantity, BranchID = branchId, ItemID = itemId }, trans);

                        // B. Log in WasteTracker
                        string sqlWaste = @"
                            INSERT INTO WasteTracker (BranchID, ItemID, QtyWasted, Reason, RecordedBy, DateRecorded)
                            VALUES (@BranchID, @ItemID, @Qty, @Reason, @UserID, GETDATE());
                            SELECT SCOPE_IDENTITY();";

                        int wasteId = conn.ExecuteScalar<int>(sqlWaste, new
                        {
                            BranchID = branchId,
                            ItemID = itemId,
                            Qty = quantity,
                            Reason = reason,
                            UserID = userId
                        }, trans);

                        // C. LOG FINANCIAL LOSS TO THE P&L
                        decimal unitCost = GetLatestItemCost(itemId, conn, trans);
                        decimal totalFinancialLoss = unitCost * quantity;

                        // Only log to Ledger if it actually has a monetary value
                        if (totalFinancialLoss > 0)
                        {
                            string sqlLedger = @"
                                INSERT INTO FinancialLedger (TransactionDate, BranchID, Type, Category, Amount, Description, ReferenceID)
                                VALUES (GETDATE(), @BranchID, 'Expense', 'Waste', @Amount, @Desc, @RefID)";

                            conn.Execute(sqlLedger, new
                            {
                                BranchID = branchId,
                                Amount = totalFinancialLoss,
                                Desc = $"Spoilage/Waste: {reason} (x{quantity})",
                                RefID = wasteId
                            }, trans);
                        }

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

        // 2. GET RECENT LOGS (For the UI List)
        public List<WasteRecord> GetRecentWaste(int branchId)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    SELECT TOP 20 w.*, m.ItemName, u.FullName as RecordedByName
                    FROM WasteTracker w
                    INNER JOIN MasterInventory m ON w.ItemID = m.ItemID
                    LEFT JOIN Users u ON w.RecordedBy = u.UserID
                    WHERE w.BranchID = @BranchID
                    ORDER BY w.DateRecorded DESC";

                return connection.Query<WasteRecord>(sql, new { BranchID = branchId }).ToList();
            }
        }

        // 3. HELPER: Get Latest Item Cost for Financial Valuation
        private decimal GetLatestItemCost(int itemId, IDbConnection conn, IDbTransaction trans)
        {
            // Fetches the most recent price we paid for this ingredient
            string sql = @"
                SELECT TOP 1 UnitPrice 
                FROM PurchaseDetails 
                WHERE ItemID = @ItemID 
                ORDER BY DetailID DESC";

            var latestPrice = conn.ExecuteScalar<decimal?>(sql, new { ItemID = itemId }, trans);

            // If we have never purchased it (e.g., test data), default to 0 to prevent crashes
            return latestPrice ?? 0m;
        }
    }
}