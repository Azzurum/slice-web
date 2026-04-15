using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using SLICE_Website.Models;

namespace SLICE_Website.Data
{
    public class InventoryRepository
    {
        private readonly DatabaseService _dbService;

        public InventoryRepository(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        // =========================================================
        // 1. MASTER INVENTORY
        // =========================================================

        public List<MasterInventory> GetAllIngredients(string search = "")
        {
            using (var connection = _dbService.GetConnection())
            {
                // The Central Warehouse view must ONLY show stock physically located at Headquarters (BranchID = 4).
                // Added m.ImagePath to the SELECT and GROUP BY clauses so the UI can load the pictures!
                string sql = @"
                SELECT 
                    m.ItemID, 
                    m.ItemName, 
                    m.Category, 
                    m.BulkUnit, 
                    m.BaseUnit, 
                    m.ConversionRatio,
                    m.ImagePath, 
                    ISNULL(SUM(b.CurrentQuantity), 0) AS TotalStock
                FROM MasterInventory m
                LEFT JOIN BranchInventory b ON m.ItemID = b.ItemID AND b.BranchID = 4
                WHERE (@Search = '' OR m.ItemName LIKE @Search OR m.Category LIKE @Search)
                GROUP BY 
                    m.ItemID, m.ItemName, m.Category, m.BulkUnit, m.BaseUnit, m.ConversionRatio, m.ImagePath
                ORDER BY m.Category, m.ItemName";

                return connection.Query<MasterInventory>(sql, new { Search = "%" + search + "%" }).AsList();
            }
        }

        public void AddIngredient(MasterInventory item)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    INSERT INTO MasterInventory (ItemName, Category, BulkUnit, BaseUnit, ConversionRatio, ImagePath) 
                    VALUES (@ItemName, @Category, @BulkUnit, @BaseUnit, @ConversionRatio, @ImagePath)";

                connection.Execute(sql, item);
            }
        }

        // =========================================================
        // 2. BRANCH INVENTORY
        // =========================================================

        public List<BranchInventoryItem> GetStockForBranch(int branchId)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    SELECT 
                        bi.StockID, 
                        bi.BranchID, 
                        bi.ItemID,  
                        bi.CurrentQuantity, 
                        bi.LowStockThreshold, 
                        bi.ExpirationDate,
                        mi.ItemName, 
                        mi.BaseUnit, 
                        mi.Category,
                        mi.ImagePath
                    FROM BranchInventory bi
                    INNER JOIN MasterInventory mi ON bi.ItemID = mi.ItemID
                    WHERE bi.BranchID = @BranchID
                    ORDER BY mi.ItemName";

                return connection.Query<BranchInventoryItem>(sql, new { BranchID = branchId }).AsList();
            }
        }

        // =========================================================
        // 3. UTILITIES
        // =========================================================

        // Get all branches
        public List<Branch> GetAllBranches()
        {
            using (var connection = _dbService.GetConnection())
            {
                return connection.Query<Branch>("SELECT * FROM Branches ORDER BY BranchName").AsList();
            }
        }

        // Get destinations for Mesh Logistics transfers
        public List<Branch> GetShippingDestinations(int myBranchId)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = "SELECT * FROM Branches WHERE BranchID != @MyId ORDER BY BranchName";
                return connection.Query<Branch>(sql, new { MyId = myBranchId }).AsList();
            }
        }

        // =========================================================
        // 4. RECONCILIATION & LEAKAGE TRACKING
        // =========================================================

        // Load data for the physical stock count sheet
        public List<ReconItem> GetReconciliationSheet(int branchId)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    SELECT 
                        bi.StockID, 
                        bi.BranchID,
                        mi.ItemID,
                        mi.ItemName, 
                        bi.CurrentQuantity as SystemQty, 
                        0 as PhysicalQty
                    FROM BranchInventory bi
                    JOIN MasterInventory mi ON bi.ItemID = mi.ItemID
                    WHERE bi.BranchID = @BranchID
                    ORDER BY mi.ItemName";

                return connection.Query<ReconItem>(sql, new { BranchID = branchId }).AsList();
            }
        }

        public void UpdateLowStockThreshold(int stockId, decimal newThreshold)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = "UPDATE BranchInventory SET LowStockThreshold = @Threshold WHERE StockID = @StockID";
                connection.Execute(sql, new { Threshold = newThreshold, StockID = stockId });
            }
        }

        // Save physical count and log financial leakage if missing items
        public void SaveAdjustment(int stockId, int branchId, int itemId, decimal systemQty, decimal physicalQty, int userId)
        {
            using (var connection = _dbService.GetConnection())
            {
                connection.Open();
                using (var trans = connection.BeginTransaction())
                {
                    try
                    {
                        decimal variance = physicalQty - systemQty;

                        // 1. Log adjustment history
                        string sqlLog = @"
                            INSERT INTO InventoryAdjustments (StockID, SystemQty, PhysicalQty, Variance, AdjustedBy, AdjustmentDate)
                            VALUES (@StockID, @Sys, @Phys, @Var, @User, GETDATE());
                            SELECT SCOPE_IDENTITY();";

                        int adjustmentId = connection.ExecuteScalar<int>(sqlLog, new
                        {
                            StockID = stockId,
                            Sys = systemQty,
                            Phys = physicalQty,
                            Var = variance,
                            User = userId
                        }, trans);

                        // 2. Update real stock levels
                        string sqlUpdate = "UPDATE BranchInventory SET CurrentQuantity = @Phys, LastUpdated = GETDATE() WHERE StockID = @StockID";
                        connection.Execute(sqlUpdate, new { Phys = physicalQty, StockID = stockId }, trans);

                        // 3. Log financial loss to P&L if items are missing
                        if (variance < 0)
                        {
                            // Get latest unit cost for exact loss calculation
                            string sqlCost = "SELECT TOP 1 UnitPrice FROM PurchaseDetails WHERE ItemID = @ItemID ORDER BY DetailID DESC";
                            var unitCost = connection.ExecuteScalar<decimal?>(sqlCost, new { ItemID = itemId }, trans) ?? 0m;

                            decimal totalLoss = Math.Abs(variance) * unitCost;

                            if (totalLoss > 0)
                            {
                                string sqlLedger = @"
                                    INSERT INTO FinancialLedger (TransactionDate, BranchID, Type, Category, Amount, Description, ReferenceID)
                                    VALUES (GETDATE(), @BranchID, 'Expense', 'Leakage', @Amount, @Desc, @RefID)";

                                connection.Execute(sqlLedger, new
                                {
                                    BranchID = branchId,
                                    Amount = totalLoss,
                                    Desc = $"Inventory Leakage: {Math.Abs(variance)} units missing",
                                    RefID = adjustmentId
                                }, trans);
                            }
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

        // Helper class for UI binding
        public class ReconItem
        {
            public int StockID { get; set; }
            public int BranchID { get; set; }
            public int ItemID { get; set; }
            public string ItemName { get; set; }
            public decimal SystemQty { get; set; }
            public decimal PhysicalQty { get; set; }
        }
    }
}