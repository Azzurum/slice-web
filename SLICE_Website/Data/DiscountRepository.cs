using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using SLICE_Website.Models;

namespace SLICE_Website.Data
{
    public class DiscountRepository
    {
        // 1. Remove the "= new DatabaseService()"
        private readonly DatabaseService _db;

        // 2. ADD THIS CONSTRUCTOR: Ask the Web API to inject the DatabaseService
        public DiscountRepository(DatabaseService db)
        {
            _db = db;
        }

        // Gets all active discounts that the current user is allowed to apply
        public List<Discount> GetAvailableDiscounts(string userRole)
        {
            using (var conn = _db.GetConnection())
            {
                // Added a 1-hour buffer (DATEADD) to StartDate.
                // This ensures that if the Azure Server clock is slightly behind your local time, 
                // a promo set to start "now" will still show up immediately.
                string sql = @"
                    SELECT * FROM Discounts 
                    WHERE IsActive = 1 
                    AND (EndDate IS NULL OR EndDate >= GETDATE())
                    AND (StartDate IS NULL OR StartDate <= DATEADD(hour, 1, GETDATE()))";

                var allDiscounts = conn.Query<Discount>(sql).ToList();

                // Robust Role Filtering:
                // If the user is a Clerk, we filter the list to only show Clerk-appropriate discounts.
                // If the user is a Manager, Owner, or Admin, we skip this filter so they see EVERYTHING.
                if (!string.IsNullOrEmpty(userRole) && userRole.Equals("Clerk", StringComparison.OrdinalIgnoreCase))
                {
                    return allDiscounts.Where(d =>
                        !string.IsNullOrEmpty(d.RequiredRole) &&
                        d.RequiredRole.Equals("Clerk", StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                }

                // Managers and Admins see the full list of active promos
                return allDiscounts;
            }
        }

        public void LogAppliedDiscount(int branchId, int discountId, int userId, decimal amount, string referenceId, string reason)
        {
            using (var conn = _db.GetConnection())
            {
                // 1. Log the audit trail
                string sqlLog = @"
                    INSERT INTO AppliedDiscounts (BranchID, DiscountID, AppliedBy, ReferenceID, Reason, CalculatedAmount, AppliedDate)
                    VALUES (@Branch, @Disc, @User, @Ref, @Reason, @Amt, GETDATE())";

                conn.Execute(sqlLog, new { Branch = branchId, Disc = discountId, User = userId, Ref = referenceId, Reason = reason, Amt = amount });

                // 2. Offset the Financial Ledger (Record the discount as an expense to balance the gross income)
                string sqlLedger = @"
                    INSERT INTO FinancialLedger (TransactionDate, BranchID, Type, Category, Amount, Description)
                    VALUES (GETDATE(), @Branch, 'Expense', 'Discount Applied', @Amt, @Desc)";

                conn.Execute(sqlLedger, new { Branch = branchId, Amt = amount, Desc = $"Discount Audit Ref: {referenceId ?? reason}" });
            }
        }

        // Gets ALL discounts for the Owner's Admin Panel
        public List<Discount> GetAllAdminDiscounts()
        {
            using (var conn = _db.GetConnection())
            {
                string sql = "SELECT * FROM Discounts ORDER BY IsActive DESC, DiscountType, DiscountName";
                return conn.Query<Discount>(sql).ToList();
            }
        }

        // Creates a new promotional or manual discount rule
        public void CreateDiscount(Discount newDiscount)
        {
            using (var conn = _db.GetConnection())
            {
                string sql = @"
            INSERT INTO Discounts (DiscountName, DiscountType, Scope, ValueType, DiscountValue, IsActive, RequiredRole, IsStackable)
            VALUES (@DiscountName, @DiscountType, @Scope, @ValueType, @DiscountValue, 1, @RequiredRole, 0)";

                conn.Execute(sql, newDiscount);
            }
        }

        // Toggles a discount on or off
        public void ToggleDiscountStatus(int discountId, bool newStatus)
        {
            using (var conn = _db.GetConnection())
            {
                string sql = "UPDATE Discounts SET IsActive = @Status WHERE DiscountID = @Id";
                conn.Execute(sql, new { Status = newStatus ? 1 : 0, Id = discountId });
            }
        }
    }
}