using Microsoft.AspNetCore.Mvc;
using SLICE_Website.Data;
using SLICE_Website.Models;
using System;

namespace SLICE_Website.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly DashboardRepository _repo;

        public DashboardController(DashboardRepository repo)
        {
            _repo = repo;
        }

        // NEW: Endpoint to get the list of branches for the dropdown
        [HttpGet("branches")]
        public IActionResult GetBranches()
        {
            return Ok(_repo.GetAllBranches());
        }

        // UPDATED: Now requires exact Start and End dates from the website
        [HttpGet("metrics")]
        public IActionResult GetDashboardMetrics([FromQuery] int? branchId, [FromQuery] DateTime start, [FromQuery] DateTime end)
        {
            try
            {
                int? filterBranch = branchId == 0 ? null : branchId;

                var metrics = _repo.GetMetrics(start, end, filterBranch);
                metrics.RecentActivity = _repo.GetRecentActivity(start, end, filterBranch);

                var alerts = _repo.GetLowStockAlerts(filterBranch);
                metrics.Alerts = new System.Collections.Generic.List<LowStockAlert>();
                foreach (var alert in alerts)
                {
                    metrics.Alerts.Add(new LowStockAlert { ItemName = alert });
                }

                return Ok(metrics);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}