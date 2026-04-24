using Microsoft.AspNetCore.Mvc;
using SLICE_Website.Data;
using SLICE_Website.Models;

namespace SLICE_Website.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiscountController : ControllerBase
    {
        private readonly DiscountRepository _repo;

        public DiscountController(DiscountRepository repo)
        {
            _repo = repo;
        }

        [HttpGet("admin")]
        public IActionResult GetAllAdminDiscounts()
        {
            try
            {
                var list = _repo.GetAllAdminDiscounts();
                return Ok(list);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // By accepting the strict Discount model here, the frontend's new 'Reason' and 'ReferenceID' fix guarantees this works.
        [HttpPost]
        public IActionResult CreateDiscount([FromBody] Discount newDiscount)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                _repo.CreateDiscount(newDiscount);
                return Ok(new { Message = "Pricing rule created successfully!" });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPut("{id}/toggle")]
        public IActionResult ToggleStatus(int id, [FromBody] ToggleRequest request)
        {
            try
            {
                _repo.ToggleDiscountStatus(id, request.NewStatus);
                return Ok(new { Message = "Status updated successfully." });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }

    public class ToggleRequest
    {
        public bool NewStatus { get; set; }
    }
}