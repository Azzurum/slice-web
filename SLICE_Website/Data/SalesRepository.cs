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
        public bool CompleteSale(int branchId, int userId, List<CartItem> cart, string paymentMethod, string referenceNumber, decimal finalDiscountedTotal, out string errorMessage)
        {
            errorMessage = string.Empty;

            using (var connection = _dbService.GetConnection())
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
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
                                    AND CurrentQuantity >= @AmountToDeduct";

                                foreach (var ing in ingredients)
                                {
                                    decimal totalNeeded = ing.RequiredQty * item.Qty;

                                    int rowsAffected = connection.Execute(sqlDeduct, new
                                    {
                                        AmountToDeduct = totalNeeded,
                                        BranchID = branchId,
                                        ItemID = ing.IngredientID
                                    }, transaction);

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
                                (@BranchID, @UserID, @ProductID, @Qty, @Price, GETDATE(), @PayMethod, @RefNum, 'Completed')";

                            connection.Execute(sqlRecord, new
                            {
                                BranchID = branchId,
                                UserID = userId,
                                ProductID = item.ProductID,
                                Qty = item.Qty,
                                Price = unitPrice,
                                PayMethod = paymentMethod,
                                RefNum = referenceNumber
                            }, transaction);
                        }

                        // --- STEP 5: FINANCIAL LEDGER ---
                        string sqlLedger = @"
                            INSERT INTO FinancialLedger (TransactionDate, BranchID, Type, Category, Amount, Description, PaymentMethod, ReferenceNumber)
                            VALUES (GETDATE(), @BranchID, 'Income', 'Sales', @Amount, @Desc, @PayMethod, @RefNum)";

                        connection.Execute(sqlLedger, new
                        {
                            BranchID = branchId,
                            Amount = finalDiscountedTotal,
                            Desc = $"POS Sale ({cart.Count} items)",
                            PayMethod = paymentMethod,
                            RefNum = referenceNumber
                        }, transaction);

                        // --- STEP 6: WRITE DIRECTLY TO AUDIT LOG ---
                        string sqlAudit = @"
                            INSERT INTO AuditLogs (UserID, ActionType, AffectedTable, NewValue, Timestamp, ReferenceNumber)
                            VALUES (@UserID, 'Sale Completed', 'SalesTransactions', @Desc, GETDATE(), @RefNum)";

                        connection.Execute(sqlAudit, new
                        {
                            UserID = userId,
                            Desc = $"Processed sale for {cart.Count} items. Gateway: {paymentMethod.ToUpper()} - Final Total: ₱{finalDiscountedTotal:N2}",
                            RefNum = referenceNumber
                        }, transaction);

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

        // =========================================================
        // 3. GET TODAY'S TRANSACTIONS (Fixed Missing Columns!)
        // =========================================================
        public List<TransactionDto> GetTodayTransactions(int branchId)
        {
            using (var conn = _dbService.GetConnection())
            {
                // Groups item-level transactions into single order tickets. 
                // Now includes Time, Item Name, Quantity, and Payment Method!
                string sql = @"
                    SELECT 
                        t.ReferenceNumber,
                        MAX(t.TransactionDate) AS LocalTransactionDate,
                        SUM(t.QuantitySold * t.UnitPrice) AS TotalAmount,
                        MAX(t.TransactionStatus) AS Status,
                        MAX(t.PaymentMethod) AS PaymentMethod,
                        SUM(t.QuantitySold) AS QuantitySold,
                        MAX(m.ProductName) AS ProductName
                    FROM SalesTransactions t
                    LEFT JOIN MenuItems m ON t.ProductID = m.ProductID
                    WHERE t.BranchID = @BranchID 
                    AND CAST(t.TransactionDate AS DATE) = CAST(GETDATE() AS DATE)
                    GROUP BY t.ReferenceNumber
                    ORDER BY MAX(t.TransactionDate) DESC";

                return conn.Query<TransactionDto>(sql, new { BranchID = branchId }).ToList();
            }
        }

        // =========================================================
        // 4. VOID TRANSACTION (Fixed Stored Procedure Crash!)
        // =========================================================
        public void VoidTransaction(string refNum)
        {
            using (var conn = _dbService.GetConnection())
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Verify the transaction exists
                        var items = conn.Query("SELECT ProductID, QuantitySold, BranchID, TransactionStatus, UnitPrice FROM SalesTransactions WHERE ReferenceNumber = @Ref", new { Ref = refNum }, trans).ToList();

                        if (!items.Any()) throw new Exception("Transaction not found.");
                        if (items.First().TransactionStatus == "Voided") throw new Exception("Transaction is already voided.");

                        // 2. Mark items as Voided
                        conn.Execute("UPDATE SalesTransactions SET TransactionStatus = 'Voided' WHERE ReferenceNumber = @Ref", new { Ref = refNum }, trans);

                        // 3. Restore Ingredients (Replaced sp_RestoreIngredients with inline SQL)
                        foreach (var item in items)
                        {
                            string sqlRestore = @"
                                UPDATE bi
                                SET bi.CurrentQuantity = bi.CurrentQuantity + (bom.RequiredQty * @Quantity)
                                FROM BranchInventory bi
                                INNER JOIN BillOfMaterials bom ON bi.ItemID = bom.ItemID
                                WHERE bom.ProductID = @ProductID AND bi.BranchID = @BranchID";

                            conn.Execute(sqlRestore, new { ProductID = item.ProductID, Quantity = item.QuantitySold, BranchID = item.BranchID }, trans);
                        }

                        // 4. Reverse Financial Ledger
                        decimal grandTotal = items.Sum(i => (decimal)i.QuantitySold * (decimal)i.UnitPrice);
                        string sqlLedger = @"
                            INSERT INTO FinancialLedger (TransactionDate, BranchID, Type, Category, Amount, Description, ReferenceNumber)
                            VALUES (GETDATE(), @BranchID, 'Expense', 'Refund', @Amount, 'VOIDED SALE', @Ref)";

                        conn.Execute(sqlLedger, new { BranchID = items.First().BranchID, Amount = grandTotal, Ref = refNum }, trans);

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

    // Smart DTO to perfectly match the UI
    public class TransactionDto
    {
        public string ReferenceNumber { get; set; } = string.Empty;
        public DateTime? LocalTransactionDate { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int? QuantitySold { get; set; }
        public decimal? TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        // UI Mapping Properties
        public DateTime DisplayDate => LocalTransactionDate ?? DateTime.Now;
        public decimal DisplayAmount => TotalAmount ?? 0m;
        public int DisplayQty => QuantitySold ?? 0;
        public string DisplayProduct => !string.IsNullOrEmpty(ProductName) ? ProductName : "Order Item(s)";
        public string DisplayMethod => !string.IsNullOrEmpty(PaymentMethod) ? PaymentMethod : "Cash";
    }
}