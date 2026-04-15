using System.Collections.Generic;
using System.Linq;
using Dapper;
using SLICE_Website.Models;

namespace SLICE_Website.Data
{
    public class AuditRepository
    {
        private readonly DatabaseService _dbService;

        public AuditRepository(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        public List<AuditEntry> GetSystemHistory(string search = "")
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    SELECT * FROM 
                    (
                        -- 1. SALES
                        SELECT 
                            s.TransactionDate as Timestamp, 
                            'SALE' as ActivityType, 
                            'Sold ' + CAST(ISNULL(s.QuantitySold, 1) AS VARCHAR) + 'x ' + ISNULL(m.ProductName, 'Unknown Item') as Description,
                            ISNULL(b.BranchName, 'Unknown Branch') as BranchName, 
                            ISNULL(u.FullName, 'Store Staff') as PerformedBy,
                            s.ReferenceNumber 
                        FROM SalesTransactions s
                        LEFT JOIN Branches b ON s.BranchID = b.BranchID
                        LEFT JOIN MenuItems m ON s.ProductID = m.ProductID
                        LEFT JOIN Users u ON s.UserID = u.UserID 

                        UNION ALL

                        -- 2. WASTE
                        SELECT 
                            w.DateRecorded as Timestamp, 
                            'WASTE' as ActivityType, 
                            'Reason: ' + w.Reason as Description, 
                            b.BranchName, 
                            u.FullName as PerformedBy,
                            NULL as ReferenceNumber 
                        FROM WasteTracker w
                        JOIN Branches b ON w.BranchID = b.BranchID
                        JOIN Users u ON w.RecordedBy = u.UserID

                        UNION ALL

                        -- 3. LOGISTICS
                        SELECT 
                            m.SentDate as Timestamp, 
                            'SHIPMENT' as ActivityType, 
                            'Transfer #' + CAST(m.TransferID AS VARCHAR) as Description, 
                            b.BranchName, 
                            u.FullName as PerformedBy,
                            NULL as ReferenceNumber 
                        FROM MeshLogistics m
                        JOIN Branches b ON m.FromBranchID = b.BranchID
                        JOIN Users u ON m.SenderID = u.UserID

                        UNION ALL

                        -- 4. DIRECT AUDIT LOGS (Z-Readings, etc.)
                        SELECT 
                            a.Timestamp,
                            UPPER(a.ActionType) as ActivityType,
                            a.NewValue as Description,
                            ISNULL(b.BranchName, 'System') as BranchName,
                            ISNULL(u.FullName, 'System User') as PerformedBy,
                            a.ReferenceNumber
                        FROM AuditLogs a
                        LEFT JOIN Users u ON a.UserID = u.UserID
                        LEFT JOIN Branches b ON u.BranchID = b.BranchID
                        WHERE a.ActionType != 'Sale Completed' -- Prevents duplicating sales from block 1
                    ) AS AllActivity
                    WHERE (@SearchStr = '' 
                           OR Description LIKE @SearchStr 
                           OR BranchName LIKE @SearchStr 
                           OR PerformedBy LIKE @SearchStr
                           OR ReferenceNumber LIKE @SearchStr) 
                    ORDER BY Timestamp DESC";

                // Pass the search term with wildcards for SQL LIKE
                return connection.Query<AuditEntry>(sql, new { SearchStr = "%" + search + "%" }).AsList();
            }
        }

        // Writes security, system, and financial events directly to the database
        public void LogAction(int userId, string actionType, string description, string referenceNumber = null)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    INSERT INTO AuditLogs (UserID, ActionType, NewValue, ReferenceNumber, Timestamp)
                    VALUES (@UserID, @ActionType, @Description, @ReferenceNumber, GETUTCDATE())";

                connection.Execute(sql, new
                {
                    UserID = userId,
                    ActionType = actionType,
                    Description = description,
                    ReferenceNumber = referenceNumber
                });
            }
        }
    }
}