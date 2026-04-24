using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SLICE_Website.Data;
using System.Linq;
using Dapper;

namespace SLICE_Website.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LogisticsController : ControllerBase
    {
        private readonly LogisticsRepository _repo;
        private readonly IConfiguration _config;

        public LogisticsController(LogisticsRepository repo, IConfiguration config)
        {
            _repo = repo;
            _config = config;
        }

        // ========================================================
        // FETCH REAL BRANCHES (EXCLUDING HEADQUARTERS)
        // ========================================================
        [HttpGet("branches")]
        public IActionResult GetBranches()
        {
            try
            {
                using (var conn = new SLICE_Website.Data.DatabaseService(_config).GetConnection())
                {
                    var branches = conn.Query("SELECT BranchID, BranchName FROM Branches WHERE BranchID != 4 ORDER BY BranchName").ToList();
                    return Ok(branches);
                }
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // ========================================================
        // DISPATCH STOCK FROM HEADQUARTERS
        // ========================================================
        [HttpPost("dispatch")]
        public IActionResult DispatchStock([FromBody] DispatchRequestDto request)
        {
            try
            {
                using (var conn = new SLICE_Website.Data.DatabaseService(_config).GetConnection())
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            foreach (var item in request.Items)
                            {
                                string sqlCheckStock = "SELECT ISNULL(CurrentQuantity, 0) FROM BranchInventory WHERE BranchID = 4 AND ItemID = @ItemID";
                                decimal currentStock = conn.ExecuteScalar<decimal>(sqlCheckStock, new { ItemID = item.ItemID }, transaction);

                                decimal totalRequired = item.Quantity * request.TargetBranchIDs.Count;

                                if (currentStock < totalRequired)
                                {
                                    return BadRequest($"Cannot dispatch {totalRequired} units. The warehouse only has {currentStock} units available for Item ID {item.ItemID}!");
                                }
                            }

                            foreach (int targetBranchId in request.TargetBranchIDs)
                            {
                                string sqlHeader = @"
                                    INSERT INTO MeshLogistics (FromBranchID, ToBranchID, Status, SenderID, SentDate)
                                    VALUES (4, @TargetBranchID, 'In-Transit', @SenderID, GETDATE());
                                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                                int transferId = conn.ExecuteScalar<int>(sqlHeader, new
                                {
                                    TargetBranchID = targetBranchId,
                                    SenderID = request.SenderID
                                }, transaction);

                                foreach (var item in request.Items)
                                {
                                    string sqlDeduct = "UPDATE BranchInventory SET CurrentQuantity = CurrentQuantity - @Qty WHERE BranchID = 4 AND ItemID = @ItemID";
                                    conn.Execute(sqlDeduct, new { ItemID = item.ItemID, Qty = item.Quantity }, transaction);

                                    string sqlDetail = "INSERT INTO WaybillDetails (TransferID, ItemID, Quantity) VALUES (@TransferID, @ItemID, @Qty);";
                                    conn.Execute(sqlDetail, new { TransferID = transferId, ItemID = item.ItemID, Qty = item.Quantity }, transaction);
                                }
                            }

                            transaction.Commit();
                            return Ok(new { Message = "Stock successfully dispatched and deducted from Warehouse." });
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // ========================================================
        // INCOMING DELIVERIES (HANDSHAKE STEP 3)
        // ========================================================
        [HttpGet("incoming/{branchId}")]
        public IActionResult GetIncomingShipments(int branchId)
        {
            try
            {
                var list = _repo.GetIncomingShipments(branchId);
                return Ok(list);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPut("receive/{transferId}")]
        public IActionResult ReceiveShipment(int transferId)
        {
            try
            {
                _repo.ReceiveShipment(transferId);
                return Ok(new { Message = "Shipment received successfully." });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // ========================================================
        // REQUEST STOCK (HANDSHAKE STEP 1)
        // ========================================================
        [HttpPost("request")]
        public IActionResult RequestStock([FromBody] StockRequestDto request)
        {
            try
            {
                var header = new SLICE_Website.Models.MeshLogistics
                {
                    FromBranchID = 4, // HEADQUARTERS
                    ToBranchID = request.ToBranchID,
                    ReceiverID = request.ReceiverID
                };

                var details = request.Items.Select(x => new SLICE_Website.Models.WaybillDetail
                {
                    ItemID = x.ItemID,
                    Quantity = x.Quantity
                }).ToList();

                _repo.RequestStock(header, details);
                return Ok(new { Message = "Stock request submitted successfully." });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // ========================================================
        // APPROVE REQUESTS (HANDSHAKE STEP 2)
        // ========================================================
        [HttpGet("pending/{branchId}")]
        public IActionResult GetPendingRequests(int branchId)
        {
            try
            {
                var list = _repo.GetPendingRequests(branchId);
                return Ok(list);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("details/{transferId}")]
        public IActionResult GetTransferDetails(int transferId)
        {
            try
            {
                var details = _repo.GetTransferDetails(transferId);
                return Ok(details);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPut("approve/{transferId}")]
        public IActionResult ApproveRequest(int transferId, [FromBody] ApproveRequestDto req)
        {
            try
            {
                _repo.ApproveRequest(transferId, req.ManagerID);
                return Ok();
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }

    // ========================================================
    // DATA TRANSFER OBJECTS (DTOs)
    // ========================================================
    public class StockRequestDto
    {
        public int ToBranchID { get; set; }
        public int ReceiverID { get; set; }
        public System.Collections.Generic.List<CartItemDto> Items { get; set; } = new();
    }

    public class CartItemDto
    {
        public int ItemID { get; set; }
        public decimal Quantity { get; set; }
    }

    public class ApproveRequestDto
    {
        public int ManagerID { get; set; }
    }

    public class DispatchRequestDto
    {
        public System.Collections.Generic.List<int> TargetBranchIDs { get; set; } = new();
        public System.Collections.Generic.List<CartItemDto> Items { get; set; } = new();
        public int SenderID { get; set; }
    }
}