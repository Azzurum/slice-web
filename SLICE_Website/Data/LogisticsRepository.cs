using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using SLICE_Website.Models;

namespace SLICE_Website.Data
{
    // Handles the "Mesh Logistics" module.
    // 1. RequestStock (Pending) 
    // 2. ApproveRequest (In-Transit) -> Deducts sender stock
    // 3. ReceiveShipment (Completed) -> Adds receiver stock
    public class LogisticsRepository
    {
        private readonly DatabaseService _dbService;

        public LogisticsRepository(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        // =========================================================
        // HANDSHAKE STEP 1: CREATE REQUEST
        // =========================================================

        public void RequestStock(MeshLogistics header, List<WaybillDetail> items)
        {
            using (var connection = _dbService.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Insert Header as Pending
                        string sqlHeader = @"
                            INSERT INTO MeshLogistics (FromBranchID, ToBranchID, Status, ReceiverID, SentDate)
                            VALUES (@FromBranchID, @ToBranchID, 'Pending', @ReceiverID, GETDATE());
                            SELECT SCOPE_IDENTITY();";

                        int newTransferId = connection.ExecuteScalar<int>(sqlHeader, header, transaction);

                        // Insert requested items
                        string sqlDetail = @"INSERT INTO WaybillDetails (TransferID, ItemID, Quantity) VALUES (@TransferID, @ItemID, @Quantity)";

                        foreach (var item in items)
                        {
                            item.TransferID = newTransferId;
                            connection.Execute(sqlDetail, item, transaction);
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        // =========================================================
        // HANDSHAKE STEP 2: APPROVE & SHIP
        // =========================================================

        // Gets requests (Pending or In-Transit)
        public List<MeshLogistics> GetPendingRequests(int myBranchId)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    SELECT m.*, 
                           b.BranchName AS ToBranchName, 
                           (SELECT SUM(Quantity) FROM WaybillDetails WHERE TransferID = m.TransferID) AS TotalQuantity
                    FROM MeshLogistics m
                    INNER JOIN Branches b ON m.ToBranchID = b.BranchID
                    WHERE m.FromBranchID = @BranchID 
                      AND m.Status IN ('Pending', 'In-Transit') 
                    ORDER BY 
                        CASE WHEN m.Status = 'Pending' THEN 1 ELSE 2 END,
                        m.SentDate";

                return connection.Query<MeshLogistics>(sql, new { BranchID = myBranchId }).ToList();
            }
        }

        public void ApproveRequest(int transferId, int managerId)
        {
            using (var connection = _dbService.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Verify record
                        string sqlHeader = "SELECT * FROM MeshLogistics WHERE TransferID = @Id";
                        var header = connection.QuerySingleOrDefault<MeshLogistics>(sqlHeader, new { Id = transferId }, transaction);

                        if (header == null) throw new Exception("Transfer record not found.");
                        if (header.Status != "Pending") throw new Exception("This request has already been processed.");

                        string sqlDetails = "SELECT * FROM WaybillDetails WHERE TransferID = @Id";
                        var items = connection.Query<WaybillDetail>(sqlDetails, new { Id = transferId }, transaction).ToList();

                        string sqlCheckStock = "SELECT CurrentQuantity FROM BranchInventory WHERE BranchID = @BranchID AND ItemID = @ItemID";
                        string sqlDeductStock = "UPDATE BranchInventory SET CurrentQuantity = CurrentQuantity - @Qty WHERE BranchID = @BranchID AND ItemID = @ItemID";

                        foreach (var item in items)
                        {
                            // Block dispatch if stock is insufficient
                            decimal currentStock = connection.ExecuteScalar<decimal>(sqlCheckStock,
                                new { BranchID = header.FromBranchID, ItemID = item.ItemID }, transaction);

                            if (currentStock < item.Quantity)
                            {
                                throw new Exception($"Insufficient stock for Item ID {item.ItemID}. Available: {currentStock}, Requested: {item.Quantity}");
                            }

                            // Deduct from sender
                            connection.Execute(sqlDeductStock,
                                new { BranchID = header.FromBranchID, ItemID = item.ItemID, Qty = item.Quantity }, transaction);
                        }

                        // Mark In-Transit
                        string sqlUpdateStatus = @"
                            UPDATE MeshLogistics 
                            SET Status = 'In-Transit', 
                                SenderID = @SenderID, 
                                SentDate = GETDATE() 
                            WHERE TransferID = @Id";

                        connection.Execute(sqlUpdateStatus, new { SenderID = managerId, Id = transferId }, transaction);

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        // =========================================================
        // HANDSHAKE STEP 3: RECEIVE SHIPMENT
        // =========================================================

        public List<MeshLogistics> GetIncomingShipments(int myBranchId)
        {
            using (var connection = _dbService.GetConnection())
            {
                // Using a LEFT JOIN so External Supplier shipments (where FromBranchID is NULL) 
                // are not accidentally hidden from the incoming deliveries list.
                string sql = @"
            SELECT m.*, 
                   ISNULL(b.BranchName, 'External Supplier') AS FromBranchName,
                   ISNULL((SELECT SUM(Quantity) FROM WaybillDetails WHERE TransferID = m.TransferID), 0) AS TotalQuantity
            FROM MeshLogistics m
            LEFT JOIN Branches b ON m.FromBranchID = b.BranchID
            WHERE m.ToBranchID = @BranchID AND m.Status = 'In-Transit'
            ORDER BY m.SentDate DESC";

                return connection.Query<MeshLogistics>(sql, new { BranchID = myBranchId }).ToList();
            }
        }

        public void ReceiveShipment(int transferId)
        {
            using (var connection = _dbService.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var items = connection.Query<WaybillDetail>("SELECT * FROM WaybillDetails WHERE TransferID = @Id", new { Id = transferId }, transaction).ToList();
                        var header = connection.QuerySingle<MeshLogistics>("SELECT * FROM MeshLogistics WHERE TransferID = @Id", new { Id = transferId }, transaction);

                        if (header.Status != "In-Transit") throw new Exception("Shipment is not in transit or already received.");

                        // FIX: Ensure inventory row exists before updating
                        string sqlEnsureStock = @"
                            IF NOT EXISTS (SELECT 1 FROM BranchInventory WHERE BranchID = @BranchID AND ItemID = @ItemID)
                            BEGIN
                                INSERT INTO BranchInventory (BranchID, ItemID, CurrentQuantity, LowStockThreshold)
                                VALUES (@BranchID, @ItemID, 0, 10)
                            END";

                        // Safely add stock
                        string sqlAddStock = @"
                            UPDATE BranchInventory 
                            SET CurrentQuantity = CurrentQuantity + @Qty, LastUpdated = GETDATE()
                            WHERE BranchID = @BranchID AND ItemID = @ItemID";

                        foreach (var item in items)
                        {
                            connection.Execute(sqlEnsureStock, new { BranchID = header.ToBranchID, ItemID = item.ItemID }, transaction);
                            connection.Execute(sqlAddStock, new { Qty = item.Quantity, BranchID = header.ToBranchID, ItemID = item.ItemID }, transaction);
                        }

                        // Mark as Completed
                        string sqlComplete = "UPDATE MeshLogistics SET Status = 'Completed', ReceivedDate = GETDATE() WHERE TransferID = @Id";
                        connection.Execute(sqlComplete, new { Id = transferId }, transaction);

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        // =========================================================
        // UTILITY METHODS
        // =========================================================

        public List<WaybillDetail> GetTransferDetails(int transferId)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    SELECT d.*, m.ItemName, m.BaseUnit 
                    FROM WaybillDetails d
                    INNER JOIN MasterInventory m ON d.ItemID = m.ItemID
                    WHERE d.TransferID = @TransferID";

                return connection.Query<WaybillDetail>(sql, new { TransferID = transferId }).ToList();
            }
        }
    }
}