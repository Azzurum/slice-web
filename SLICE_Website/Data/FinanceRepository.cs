using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using SLICE_Website.Models;

namespace SLICE_Website.Data
{
    public class FinanceRepository
    {
        private readonly DatabaseService _dbService;

        public FinanceRepository(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        public DashboardMetrics GetPnLMetrics(DateTime startDate, DateTime endDate, int? branchId = null)
        {
            using (var conn = _dbService.GetConnection())
            {
                // Dynamic SQL for Branch Filtering
                string branchFilter = branchId.HasValue ? "AND BranchID = @Bid" : "";

                string sql = $@"
                    SELECT 
                        ISNULL(SUM(CASE WHEN Type = 'Income' THEN Amount ELSE 0 END), 0) as TotalRevenue,
                        
                        -- Total Expenses (Everything)
                        ISNULL(SUM(CASE WHEN Type = 'Expense' THEN Amount ELSE 0 END), 0) as TotalExpenses,
                        
                        -- Specifically isolate Waste for the UI Banner
                        ISNULL(SUM(CASE WHEN Type = 'Expense' AND Category = 'Waste' THEN Amount ELSE 0 END), 0) as TotalWasteCost
                    FROM FinancialLedger
                    WHERE TransactionDate BETWEEN @Start AND @End {branchFilter}";

                return conn.QuerySingleOrDefault<DashboardMetrics>(sql, new { Start = startDate, End = endDate, Bid = branchId })
                       ?? new DashboardMetrics();
            }
        }

        public List<FinancialLedger> GetRecentTransactions()
        {
            using (var conn = _dbService.GetConnection())
            {
                // Get last 20 transactions
                return conn.Query<FinancialLedger>(@"
                    SELECT TOP 20 * FROM FinancialLedger 
                    ORDER BY TransactionDate DESC").ToList();
            }
        }

        // Returns data for a 'Revenue vs Expense' per Branch chart
        public List<BranchPerformance> GetBranchPerformance(DateTime startDate, DateTime endDate)
        {
            using (var conn = _dbService.GetConnection())
            {
                string sql = @"
                    SELECT 
                        b.BranchName,
                        ISNULL(SUM(CASE WHEN f.Type = 'Income' THEN f.Amount ELSE 0 END), 0) as Revenue,
                        ISNULL(SUM(CASE WHEN f.Type = 'Expense' THEN f.Amount ELSE 0 END), 0) as Expense
                    FROM Branches b
                    LEFT JOIN FinancialLedger f ON b.BranchID = f.BranchID 
                        AND f.TransactionDate BETWEEN @Start AND @End
                    GROUP BY b.BranchName";

                return conn.Query<BranchPerformance>(sql, new { Start = startDate, End = endDate }).ToList();
            }
        }
    }

    // Helper Helper Class for the Chart
    public class BranchPerformance
    {
        public string BranchName { get; set; }
        public decimal Revenue { get; set; }
        public decimal Expense { get; set; }
        public decimal NetProfit => Revenue - Expense;
    }
}