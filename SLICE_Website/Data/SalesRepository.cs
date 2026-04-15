using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using SLICE_Website.Models;

namespace SLICE_Website.Data
{
    public class SalesRepository
    {
        private readonly DatabaseService _dbService;

        public SalesRepository(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        // =========================================================
        // 1. GET MENU (With Recipe-Driven Depletion Engine)
        // =========================================================
        public List<MenuProduct> GetMenu(int branchId)
        {
            using (var connection = _dbService.GetConnection())
            {
                // This runs a precise, isolated subquery for every individual menu item.
                string sql = @"
                SELECT 
                    m.ProductID, 
                    m.ProductName, 
                    m.BasePrice, 
                    m.ImagePath,
                    'General' AS Category,
                    ISNULL((
                        SELECT CAST(MIN(FLOOR(CAST(ISNULL(bi.CurrentQuantity, 0) AS FLOAT) / bom.RequiredQty)) AS INT)
                        FROM BillOfMaterials bom
                        LEFT JOIN BranchInventory bi ON bom.ItemID = bi.ItemID AND bi.BranchID = @BranchID
                        WHERE bom.ProductID = m.ProductID AND bom.RequiredQty > 0
                    ), 0) AS MaxCookable
                FROM MenuItems m
                WHERE m.IsAvailable = 1
                ORDER BY m.ProductName ASC;";

                return connection.Query<MenuProduct>(sql, new { BranchID = branchId }).ToList();
            }
        }

        // =========================================================
        // 2. COMPLETE SALE (Entire Cart + Audit/Payment Integration)
        // =========================================================
        // [FIX APPLIED]: Added 'decimal finalDiscountedTotal' so the dashboard gets the true discounted revenue
        public bool CompleteSale(int branchId, int userId, List<CartItem> cart, string paymentMethod, string referenceNumber, decimal finalDiscountedTotal, out string errorMessage)
        {
            errorMessage = string.Empty;

            using (var connection = _dbService.GetConnection())
            {
                connection.Open();

                // Start a transaction so the whole cart succeeds or fails together (Atomic Transaction)
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Loop through every item in the shopping cart
                        foreach (var item in cart)
                        {
                            // --- STEP 1: GET PRODUCT PRICE SNAPSHOT ---
                            string sqlGetProduct = "SELECT ProductID, ProductName, BasePrice FROM MenuItems WHERE ProductID = @Id";
                            var product = connection.QuerySingleOrDefault<MenuItem>(sqlGetProduct, new { Id = item.ProductID }, transaction);

                            if (product == null) throw new Exception($"Product '{item.ProductName}' not found or invalid.");

                            decimal unitPrice = product.BasePrice;

                            // --- STEP 2: CALCULATE INGREDIENTS (Bill of Materials) ---
                            string sqlGetRecipe = "SELECT ProductID, ItemID as IngredientID, RequiredQty FROM BillOfMaterials WHERE ProductID = @ProductID";
                            var ingredients = connection.Query<Recipe>(sqlGetRecipe, new { ProductID = item.ProductID }, transaction).AsList();

                            // --- STEP 3: DEDUCT STOCK (With Negative Stock Prevention) ---
                            if (ingredients.Any())
                            {
                                string sqlDeduct = @"
                                    UPDATE BranchInventory 
                                    SET CurrentQuantity = CurrentQuantity - @AmountToDeduct
                                    WHERE BranchID = @BranchID AND ItemID = @ItemID 
                                    AND CurrentQuantity >= @AmountToDeduct"; // Safety check

                                foreach (var ing in ingredients)
                                {
                                    decimal totalNeeded = ing.RequiredQty * item.Qty;

                                    int rowsAffected = connection.Execute(sqlDeduct, new
                                    {
                                        AmountToDeduct = totalNeeded,
                                        BranchID = branchId,
                                        ItemID = ing.IngredientID
                                    }, transaction);

                                    // If 0 rows were updated, it means stock was insufficient
                                    if (rowsAffected == 0)
                                    {
                                        throw new Exception($"Transaction blocked: Insufficient stock for an ingredient required to make {product.ProductName}.");
                                    }
                                }
                            }

                            // --- STEP 4: RECORD THE SALE (Item Level) ---
                            string sqlRecord = @"
                                INSERT INTO SalesTransactions 
                                (BranchID, UserID, ProductID, QuantitySold, UnitPrice, TransactionDate, PaymentMethod, ReferenceNumber, TransactionStatus)
                                VALUES 
                                (@BranchID, @UserID, @ProductID, @Qty, @Price, GETDATE(), @PayMethod, @RefNum, 'TransactionStatus')";

                            connection.Execute(sqlRecord, new
                            {
                                BranchID = branchId,
                                UserID = userId,
                                ProductID = item.ProductID,
                                Qty = item.Qty,
                                Price = unitPrice, // Save standard item price here for item-level data
                                PayMethod = paymentMethod,
                                RefNum = referenceNumber
                            }, transaction);
                        }

                        // --- STEP 5: FINANCIAL LEDGER (Centralized Income Tracking) ---
                        // [FIX APPLIED]: Write the actual discounted total to the ledger so the Dashboard sees it
                        string sqlLedger = @"
                            INSERT INTO FinancialLedger (TransactionDate, BranchID, Type, Category, Amount, Description, PaymentMethod, ReferenceNumber)
                            VALUES (GETDATE(), @BranchID, 'Income', 'Sales', @Amount, @Desc, @PayMethod, @RefNum)";

                        connection.Execute(sqlLedger, new
                        {
                            BranchID = branchId,
                            Amount = finalDiscountedTotal, // <--- Using the true final total
                            Desc = $"POS Sale ({cart.Count} items)",
                            PayMethod = paymentMethod,
                            RefNum = referenceNumber
                        }, transaction);

                        // --- STEP 6: WRITE DIRECTLY TO AUDIT LOG ---
                        // [FIX APPLIED]: Mention the final discounted total in the audit log for clarity
                        string sqlAudit = @"
                            INSERT INTO AuditLogs (UserID, ActionType, AffectedTable, NewValue, Timestamp, ReferenceNumber)
                            VALUES (@UserID, 'Sale Completed', 'SalesTransactions', @Desc, GETDATE(), @RefNum)";

                        connection.Execute(sqlAudit, new
                        {
                            UserID = userId,
                            Desc = $"Processed sale for {cart.Count} items. Gateway: {paymentMethod.ToUpper()} - Final Total: ₱{finalDiscountedTotal:N2}",
                            RefNum = referenceNumber
                        }, transaction);

                        // Success! Everything commits to the DB at once.
                        transaction.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        // Rollback ensures that if one item fails, NO ingredients are deducted and NO sale is recorded.
                        transaction.Rollback();
                        errorMessage = ex.Message;
                        return false;
                    }
                }
            }
        }

        // Fetches only CASH sales for the logged-in user for today
        public decimal GetTodayExpectedCash(int userId)
        {
            using (var connection = _dbService.GetConnection())
            {
                // NOTE: This still sums based on base unit price. If you want Z-Reading to reflect discounted cash, 
                // you may need to adjust this to read from FinancialLedger instead of SalesTransactions in the future.
                string sql = @"
                SELECT ISNULL(SUM(QuantitySold * UnitPrice), 0) 
                FROM SalesTransactions 
                WHERE UserID = @UserID 
                AND PaymentMethod = 'Cash' 
                AND TransactionStatus = 'Completed'
                AND CAST(TransactionDate AS DATE) = CAST(GETDATE() AS DATE)";

                return connection.ExecuteScalar<decimal>(sql, new { UserID = userId });
            }
        }

        // =========================================================
        // 3. FETCH TODAY'S COMPLETED SALES (For Voiding)
        // =========================================================
        public List<SaleRecord> GetTodaySales(int branchId)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    SELECT s.SaleID, m.ProductName, s.QuantitySold, 
                           (s.QuantitySold * s.UnitPrice) as TotalAmount, 
                           s.ReferenceNumber, s.PaymentMethod, s.TransactionDate, s.TransactionStatus
                    FROM SalesTransactions s
                    JOIN MenuItems m ON s.ProductID = m.ProductID
                    WHERE s.BranchID = @BranchId 
                      AND s.TransactionStatus = 'Completed' 
                      AND CAST(s.TransactionDate AS DATE) = CAST(GETDATE() AS DATE)
                    ORDER BY s.TransactionDate DESC";

                return connection.Query<SaleRecord>(sql, new { BranchId = branchId }).ToList();
            }
        }

        // =========================================================
        // 4. VOID TRANSACTION (Atomic Reversal)
        // =========================================================
        public bool VoidSale(int saleId, int managerId, string reason, out string errorMessage)
        {
            errorMessage = string.Empty;
            using (var connection = _dbService.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Get original sale details
                        var sale = connection.QuerySingleOrDefault<dynamic>(
                            "SELECT BranchID, ProductID, QuantitySold, (QuantitySold * UnitPrice) as TotalAmount, ReferenceNumber, PaymentMethod FROM SalesTransactions WHERE SaleID = @SaleID",
                            new { SaleID = saleId }, transaction);

                        if (sale == null) throw new Exception("Transaction not found.");

                        // 2. Mark as Voided
                        connection.Execute("UPDATE SalesTransactions SET TransactionStatus = 'Cancelled' WHERE SaleID = @SaleID", new { SaleID = saleId }, transaction);

                        // 3. Return Ingredients to Inventory
                        var ingredients = connection.Query<Recipe>("SELECT ItemID as IngredientID, RequiredQty FROM BillOfMaterials WHERE ProductID = @ProductID", new { ProductID = sale.ProductID }, transaction).AsList();
                        foreach (var ing in ingredients)
                        {
                            connection.Execute(
                                "UPDATE BranchInventory SET CurrentQuantity = CurrentQuantity + @AmountToAdd WHERE BranchID = @BranchID AND ItemID = @ItemID",
                                new { AmountToAdd = (ing.RequiredQty * sale.QuantitySold), BranchID = sale.BranchID, ItemID = ing.IngredientID }, transaction);
                        }

                        // 4. Issue a Refund in the Financial Ledger
                        connection.Execute(@"
                            INSERT INTO FinancialLedger (TransactionDate, BranchID, Type, Category, Amount, Description, PaymentMethod, ReferenceNumber)
                            VALUES (GETDATE(), @BranchID, 'Expense', 'Refund', @Amount, @Desc, @PayMethod, @RefNum)",
                            new { BranchID = sale.BranchID, Amount = sale.TotalAmount, Desc = $"VOIDED SALE: {reason}", PayMethod = sale.PaymentMethod, RefNum = sale.ReferenceNumber }, transaction);

                        // 5. Log the Manager Override
                        connection.Execute(@"
                            INSERT INTO AuditLogs (UserID, ActionType, AffectedTable, NewValue, Timestamp, ReferenceNumber)
                            VALUES (@UserID, 'MANAGER OVERRIDE: VOID', 'SalesTransactions', @Desc, GETDATE(), @RefNum)",
                            new { UserID = managerId, Desc = $"Voided Sale #{saleId}. Reason: {reason}. Refunded ₱{sale.TotalAmount:N2}", RefNum = sale.ReferenceNumber }, transaction);

                        transaction.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        errorMessage = ex.Message;
                        return false;
                    }
                }
            }
        }
    }
}