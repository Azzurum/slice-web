using Microsoft.AspNetCore.Mvc;
using SLICE_Website.Data; // Ensure this matches your Repository namespace

namespace SLICE_System.Api.Controllers
{
    // 1. These "Attributes" tell the app this is an API that talks to the web
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        // 2. Create a private variable to hold your repository
        private readonly InventoryRepository _inventoryRepo;

        // 3. CONSTRUCTOR INJECTION
        // When the web API receives a request, it automatically creates this 
        // Controller and hands it the InventoryRepository we registered in Step 1.
        public InventoryController(InventoryRepository inventoryRepo)
        {
            _inventoryRepo = inventoryRepo;
        }

        // 4. THE GET ENDPOINT
        // This attribute tells the API to listen for GET requests at:
        // api/inventory/branch/{branchId} (e.g., api/inventory/branch/1)
        [HttpGet("branch/{branchId}")]
        public IActionResult GetBranchInventory(int branchId)
        {
            try
            {
                // Call your existing repository method!
                // IMPORTANT: Ensure "GetInventoryByBranchId" perfectly matches 
                // the method name inside your actual InventoryRepository.cs file.
                var inventoryList = _inventoryRepo.GetStockForBranch(branchId);

                // Check if data exists
                if (inventoryList == null)
                {
                    return NotFound(); // Sends a 404 HTTP Error code
                }

                // Send the data back as JSON
                return Ok(inventoryList); // Sends a 200 OK code + the data!
            }
            catch (System.Exception ex)
            {
                // If the database crashes, send a 500 Internal Server Error
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }
    } // <-- Notice the class closes down here now!
}