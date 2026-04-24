using Microsoft.AspNetCore.Mvc;
using SLICE_Website.Data;
using System;
using System.Linq;

namespace SLICE_Website.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuditController : ControllerBase
    {
        private readonly AuditRepository _repo;

        public AuditController(AuditRepository repo)
        {
            _repo = repo;
        }

        [HttpGet]
        public IActionResult GetAuditLogs(
            [FromQuery] string search = "",
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                // 1. Fetch raw logs using the exact WPF Repository logic
                var rawLogs = _repo.GetSystemHistory(search ?? "");

                // 2. Apply exact Date Range Filtering from WPF ViewModel
                if (startDate.HasValue)
                {
                    rawLogs = rawLogs.Where(l => l.Timestamp.Date >= startDate.Value.Date).ToList();
                }

                if (endDate.HasValue)
                {
                    rawLogs = rawLogs.Where(l => l.Timestamp.Date <= endDate.Value.Date).ToList();
                }

                // 3. Limit to top 200 to prevent RAM overflow on massive databases (Exact WPF logic)
                var finalLogs = rawLogs.Take(200).ToList();

                return Ok(finalLogs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Database Error: " + ex.Message);
            }
        }
    }
}