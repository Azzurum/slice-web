using Microsoft.AspNetCore.Mvc;
using SLICE_Website.Data;
using System;

namespace SLICE_Website.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WasteController : ControllerBase
    {
        private readonly WasteRepository _repo;

        public WasteController(WasteRepository repo)
        {
            _repo = repo;
        }

        [HttpGet("recent/{branchId}")]
        public IActionResult GetRecentWaste(int branchId)
        {
            try
            {
                return Ok(_repo.GetRecentWaste(branchId));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("record")]
        public IActionResult RecordWaste([FromBody] WasteLogRequest req)
        {
            try
            {
                _repo.RecordWaste(req.BranchID, req.ItemID, req.Qty, req.Reason, req.UserID);
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // A simple DTO (Data Transfer Object) to catch the incoming JSON
        public class WasteLogRequest
        {
            public int BranchID { get; set; }
            public int ItemID { get; set; }
            public decimal Qty { get; set; }
            public string Reason { get; set; } = string.Empty;
            public int UserID { get; set; }
        }
    }
}