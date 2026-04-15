using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using SLICE_Website.Models;

namespace SLICE_Website.Data
{
    public class DashboardRepository
    {
        private readonly DatabaseService _dbService;

        public DashboardRepository(DatabaseService dbService)
        {
            _dbService = dbService;
        }
        public List<Branch> GetAllBranches()
        {
            using (var connection = _dbService.GetConnection())
            {
                    return connection.Query<Branch>("SELECT BranchID, BranchName FROM Branches").ToList();
            }
        }

        public DashboardMetrics GetMetrics(DateTime start, DateTime end, int? branchId)
        {
            using (var connection = _dbService.GetConnection())
            {
                var metrics = new DashboardMetrics();
                var p = new { Start = start, End = end, BranchID = branchId };

                // 1. FINANCIAL AGGREGATION (From Financial Ledger)
                string sqlFinance = @"
                    SELECT 
                        ISNULL(SUM(CASE WHEN Type = 'Income' THEN Amount ELSE 0 END), 0) AS TotalRevenue,
                        ISNULL(SUM(CASE WHEN Type = 'Expense' THEN Amount ELSE 0 END), 0) AS TotalExpenses,
                        ISNULL(SUM(CASE WHEN Category IN ('Waste', 'Leakage') THEN Amount ELSE 0 END), 0) AS TotalWasteCost
                    FROM FinancialLedger
                    WHERE (TransactionDate BETWEEN @Start AND @End)
                    AND (@BranchID IS NULL OR BranchID = @BranchID)";

                var financeData = connection.QueryFirstOrDefault(sqlFinance, p);
                if (financeData != null)
                {
                    metrics.TotalRevenue = financeData.TotalRevenue;
                    metrics.TotalExpenses = financeData.TotalExpenses;
                    metrics.TotalWasteCost = financeData.TotalWasteCost;
                }

                // 2. OPERATIONAL KPIs
                string sqlSalesCount = @"
                    SELECT ISNULL(COUNT(SaleID), 0) 
                    FROM SalesTransactions 
                    WHERE (TransactionDate BETWEEN @Start AND @End) 
                    AND (@BranchID IS NULL OR BranchID = @BranchID)";
                metrics.TotalSalesCount = connection.ExecuteScalar<int>(sqlSalesCount, p);

                string sqlAlertCount = @"
                    SELECT COUNT(*) FROM BranchInventory 
                    WHERE CurrentQuantity <= LowStockThreshold 
                    AND (@BranchID IS NULL OR BranchID = @BranchID)";
                metrics.LowStockCount = connection.ExecuteScalar<int>(sqlAlertCount, p);

                string sqlShip = @"
                    SELECT COUNT(*) FROM MeshLogistics 
                    WHERE (Status = 'In-Transit' OR Status = 'Pending') 
                    AND (@BranchID IS NULL OR ToBranchID = @BranchID)";
                metrics.PendingShipments = connection.ExecuteScalar<int>(sqlShip, p);

                // 3. TOP PRODUCTS
                string sqlProducts = @"
                    SELECT TOP 5 m.ProductName, ISNULL(SUM(s.QuantitySold), 0) as QuantitySold
                    FROM SalesTransactions s
                    JOIN MenuItems m ON s.ProductID = m.ProductID
                    WHERE (s.TransactionDate BETWEEN @Start AND @End)
                    AND (@BranchID IS NULL OR s.BranchID = @BranchID)
                    GROUP BY m.ProductName
                    ORDER BY QuantitySold DESC";
                metrics.TopProducts = connection.Query<ProductMix>(sqlProducts, p).AsList();

                // 4. ALERTS GRID
                string sqlAlerts = @"
                    SELECT TOP 20 b.BranchName, mi.ItemName, bi.CurrentQuantity as CurrentQty, bi.LowStockThreshold as Threshold
                    FROM BranchInventory bi
                    JOIN Branches b ON bi.BranchID = b.BranchID
                    JOIN MasterInventory mi ON bi.ItemID = mi.ItemID
                    WHERE bi.CurrentQuantity <= bi.LowStockThreshold
                    AND (@BranchID IS NULL OR b.BranchID = @BranchID)
                    ORDER BY bi.CurrentQuantity ASC";
                metrics.Alerts = connection.Query<LowStockAlert>(sqlAlerts, p).AsList();

                // 5. CHART: Branch Performance (Revenue vs Expense)
                string sqlBranchPerf = @"
                    SELECT TOP 5 
                        b.BranchName, 
                        ISNULL(SUM(CASE WHEN f.Type = 'Income' THEN f.Amount ELSE 0 END), 0) as TotalRevenue,
                        ISNULL(SUM(CASE WHEN f.Type = 'Expense' THEN f.Amount ELSE 0 END), 0) as TotalExpense
                    FROM Branches b
                    LEFT JOIN FinancialLedger f ON b.BranchID = f.BranchID AND (f.TransactionDate BETWEEN @Start AND @End)
                    WHERE (@BranchID IS NULL OR b.BranchID = @BranchID)
                    GROUP BY b.BranchName
                    ORDER BY TotalRevenue DESC";
                metrics.BranchRanking = connection.Query<SLICE_Website.Models.BranchPerformance>(sqlBranchPerf, p).AsList();

                return metrics;
            }
        }

        public List<RecentTransaction> GetRecentActivity(DateTime start, DateTime end, int? branchId)
        {
            using (var connection = _dbService.GetConnection())
            {
                var p = new { Start = start, End = end, BranchID = branchId };
                string sql = @"
                    SELECT TOP 15 
                        s.SaleID, b.BranchName, m.ProductName, s.QuantitySold, s.TransactionDate,
                        (s.QuantitySold * m.BasePrice) as TotalAmount
                    FROM SalesTransactions s
                    JOIN Branches b ON s.BranchID = b.BranchID
                    JOIN MenuItems m ON s.ProductID = m.ProductID
                    WHERE (s.TransactionDate BETWEEN @Start AND @End)
                    AND (@BranchID IS NULL OR s.BranchID = @BranchID)
                    ORDER BY s.TransactionDate DESC";

                return connection.Query<RecentTransaction>(sql, p).AsList();
            }
        }

        public List<string> GetLowStockAlerts(int? branchId)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                SELECT b.BranchName + ': ' + mi.ItemName + ' (Only ' + CAST(bi.CurrentQuantity AS VARCHAR) + ' left!)'
                FROM BranchInventory bi
                JOIN MasterInventory mi ON bi.ItemID = mi.ItemID
                JOIN Branches b ON bi.BranchID = b.BranchID
                WHERE bi.CurrentQuantity <= bi.LowStockThreshold 
                AND (@BranchId IS NULL OR bi.BranchID = @BranchId)
                ORDER BY bi.CurrentQuantity ASC";

                return connection.Query<string>(sql, new { BranchId = branchId }).AsList();
            }
        }
    }
}