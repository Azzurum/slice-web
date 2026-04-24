using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SLICE_Website.Data;
using System.Collections.Generic;
using Dapper;
using System;
using System.IO;

namespace SLICE_Website.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly InventoryRepository _inventoryRepo;
        private readonly IConfiguration _config;

        public InventoryController(InventoryRepository inventoryRepo, IConfiguration config)
        {
            _inventoryRepo = inventoryRepo;
            _config = config;
        }

        // Retrieves current stock quantities for a specific branch
        [HttpGet("branch/{branchId}")]
        public IActionResult GetBranchInventory(int branchId)
        {
            try
            {
                var inventoryList = _inventoryRepo.GetStockForBranch(branchId);
                if (inventoryList == null) return NotFound();
                return Ok(inventoryList);
            }
            catch (Exception ex) { return StatusCode(500, "Internal server error: " + ex.Message); }
        }

        // Retrieves the worksheet used for physical inventory audits
        [HttpGet("recon/{branchId}")]
        public IActionResult GetReconciliationSheet(int branchId)
        {
            try
            {
                var sheet = _inventoryRepo.GetReconciliationSheet(branchId);
                return Ok(sheet);
            }
            catch (Exception ex) { return StatusCode(500, "Database Error: " + ex.Message); }
        }

        // Processes physical count adjustments and logs any variances found
        [HttpPost("adjust")]
        public IActionResult SaveAdjustments([FromBody] List<AdjustmentRequest> adjustments)
        {
            try
            {
                int variancesFound = 0;
                foreach (var item in adjustments)
                {
                    if (item.PhysicalQty != item.SystemQty)
                    {
                        _inventoryRepo.SaveAdjustment(item.StockID, item.BranchID, item.ItemID, item.SystemQty, item.PhysicalQty, item.UserId);
                        variancesFound++;
                    }
                }
                return Ok(new { Message = $"Inventory Audit Completed. {variancesFound} variances logged.", Variances = variancesFound });
            }
            catch (Exception ex) { return StatusCode(500, "Database Error: " + ex.Message); }
        }

        // Fetches all raw materials registered in the Central Warehouse
        [HttpGet]
        public IActionResult GetAllIngredients([FromQuery] string search = "")
        {
            try
            {
                var list = _inventoryRepo.GetAllIngredients(search ?? "");
                return Ok(list);
            }
            catch (Exception ex) { return StatusCode(500, "Database Error: " + ex.Message); }
        }

        // Safely deletes an ingredient only if it has zero stock and no active recipes
        [HttpDelete("{id}")]
        public IActionResult DeleteIngredient(int id)
        {
            try
            {
                using (var conn = new DatabaseService(_config).GetConnection())
                {
                    string sqlCheck = @"
                        SELECT 
                            (SELECT COUNT(*) FROM BillOfMaterials WHERE ItemID = @Id) +
                            (SELECT COUNT(*) FROM BranchInventory WHERE ItemID = @Id AND CurrentQuantity > 0) 
                        AS UsageCount";

                    int usage = conn.ExecuteScalar<int>(sqlCheck, new { Id = id });
                    if (usage > 0) return BadRequest("Cannot delete because it is used in a Menu Recipe or still has remaining physical stock.");

                    conn.Execute("DELETE FROM BranchInventory WHERE ItemID = @Id", new { Id = id });
                    conn.Execute("DELETE FROM MasterInventory WHERE ItemID = @Id", new { Id = id });
                }
                return Ok(new { Message = "Ingredient successfully removed." });
            }
            catch (Exception ex) { return StatusCode(500, "Database Error: " + ex.Message); }
        }

        // Updates the low stock warning trigger for an item
        [HttpPut("threshold/{stockId}")]
        public IActionResult UpdateThreshold(int stockId, [FromBody] ThresholdUpdate update)
        {
            try
            {
                _inventoryRepo.UpdateLowStockThreshold(stockId, update.NewThreshold);
                return Ok(new { Message = "Threshold updated." });
            }
            catch (Exception ex) { return StatusCode(500, "Database Error: " + ex.Message); }
        }

        // Executes a secure SQL transaction to record purchases and update HQ stock
        [HttpPost("purchase")]
        public IActionResult RecordPurchase([FromBody] PurchasePayloadDto request)
        {
            try
            {
                using (var conn = new DatabaseService(_config).GetConnection())
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            foreach (var item in request.Items)
                            {
                                string sqlUpsert = @"
                                    IF EXISTS (SELECT 1 FROM BranchInventory WHERE BranchID = 4 AND ItemID = @ItemID)
                                        UPDATE BranchInventory SET CurrentQuantity = CurrentQuantity + @Qty WHERE BranchID = 4 AND ItemID = @ItemID;
                                    ELSE
                                        INSERT INTO BranchInventory (BranchID, ItemID, CurrentQuantity) VALUES (4, @ItemID, @Qty);";

                                conn.Execute(sqlUpsert, new { ItemID = item.ItemID, Qty = item.Quantity }, transaction);
                            }

                            string auditDesc = $"PROCUREMENT: Received stock from {request.SupplierName}. Total Cost: ₱{request.GrandTotal:N2}";
                            string sqlAudit = @"
                                INSERT INTO AuditLogs (UserID, ActionType, NewValue, Timestamp)
                                VALUES (@UserID, 'PURCHASE', @Description, GETUTCDATE())";

                            conn.Execute(sqlAudit, new { UserID = request.UserID, Description = auditDesc }, transaction);

                            transaction.Commit();
                            return Ok(new { Message = "Purchase recorded and stock updated." });
                        }
                        catch { transaction.Rollback(); throw; }
                    }
                }
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        // Adds or edits a raw ingredient and securely handles Base64 image uploads via JSON
        [HttpPost("ingredient")]
        [Consumes("application/json")]
        public IActionResult SaveIngredient([FromBody] IngredientPayloadDto request)
        {
            try
            {
                string fileName = null;

                // 1. Decode the Base64 image and save it to the FRONTEND project folder
                if (!string.IsNullOrEmpty(request.ImageBase64))
                {
                    fileName = Guid.NewGuid().ToString() + (request.ImageExtension ?? ".jpg");

                    // The magic fix: Navigate up one folder, then explicitly into SLICE_Frontend
                    string frontendPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "SLICE_Frontend", "wwwroot", "Images", "Ingredients");

                    // Resolves the exact absolute path on your hard drive
                    var uploadsFolder = Path.GetFullPath(frontendPath);

                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    var filePath = Path.Combine(uploadsFolder, fileName);
                    byte[] imageBytes = Convert.FromBase64String(request.ImageBase64);
                    System.IO.File.WriteAllBytes(filePath, imageBytes);
                }

                // 2. Execute Database Logic
                using (var conn = new DatabaseService(_config).GetConnection())
                {
                    if (request.ItemID == 0)
                    {
                        int count = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM MasterInventory WHERE ItemName = @Name", new { Name = request.ItemName });
                        if (count > 0) return BadRequest("An ingredient with this exact name already exists!");

                        string sql = @"INSERT INTO MasterInventory (ItemName, Category, BulkUnit, BaseUnit, ConversionRatio, ImagePath) 
                                       VALUES (@Name, @Cat, @Bulk, @Base, @Ratio, @Img)";

                        conn.Execute(sql, new
                        {
                            Name = request.ItemName,
                            Cat = request.Category,
                            Bulk = request.BulkUnit,
                            Base = request.BaseUnit,
                            Ratio = request.ConversionRatio,
                            Img = fileName
                        });
                    }
                    else
                    {
                        string sql = @"UPDATE MasterInventory 
                                       SET ItemName = @Name, Category = @Cat, BulkUnit = @Bulk, 
                                           BaseUnit = @Base, ConversionRatio = @Ratio, 
                                           ImagePath = ISNULL(@Img, ImagePath) 
                                       WHERE ItemID = @ID";

                        conn.Execute(sql, new
                        {
                            Name = request.ItemName,
                            Cat = request.Category,
                            Bulk = request.BulkUnit,
                            Base = request.BaseUnit,
                            Ratio = request.ConversionRatio,
                            Img = fileName,
                            ID = request.ItemID
                        });
                    }
                }

                return Ok(new { Message = "Ingredient successfully saved." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Database Error: " + ex.Message);
            }
        }
    }
        // ==========================================
        // DATA TRANSFER OBJECTS (DTOs)
        // ==========================================

        public class ThresholdUpdate { public decimal NewThreshold { get; set; } }

    public class AdjustmentRequest
    {
        public int StockID { get; set; }
        public int BranchID { get; set; }
        public int ItemID { get; set; }
        public decimal SystemQty { get; set; }
        public decimal PhysicalQty { get; set; }
        public int UserId { get; set; }
    }

    public class PurchasePayloadDto
    {
        public string SupplierName { get; set; } = string.Empty;
        public DateTime PurchaseDate { get; set; }
        public decimal GrandTotal { get; set; }
        public int UserID { get; set; }
        public List<PurchaseItemPayload> Items { get; set; } = new();
    }

    public class PurchaseItemPayload
    {
        public int ItemID { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class IngredientPayloadDto
    {
        public int ItemID { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string BulkUnit { get; set; } = string.Empty;
        public string BaseUnit { get; set; } = string.Empty;
        public decimal ConversionRatio { get; set; }
        public string? ImageBase64 { get; set; }
        public string? ImageExtension { get; set; }
    }
}