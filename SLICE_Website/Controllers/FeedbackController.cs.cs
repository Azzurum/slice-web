using Microsoft.AspNetCore.Mvc;
using SLICE_Website.Data;
using SLICE_Website.Models;

namespace SLICE_Website.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FeedbackController : ControllerBase
    {
        private readonly SuggestionRepository _repo;

        // Ensure SuggestionRepository is registered in your backend Program.cs!
        public FeedbackController(SuggestionRepository repo)
        {
            _repo = repo;
        }

        [HttpGet]
        public IActionResult GetAllSuggestions()
        {
            try
            {
                var list = _repo.GetAllSuggestions();
                return Ok(list);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPut("{id}/status")]
        public IActionResult UpdateStatus(int id, [FromBody] StatusUpdateRequest request)
        {
            try
            {
                _repo.UpdateSuggestionStatus(id, request.NewStatus, request.Notes);
                return Ok();
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // ========================================================
        // SUBMIT NEW FEEDBACK
        // ========================================================
        [HttpPost]
        public IActionResult SubmitSuggestion([FromBody] SubmitSuggestionDto dto)
        {
            try
            {
                var suggestion = new CustomerSuggestion
                {
                    SuggestionType = dto.SuggestionType,
                    Description = dto.Description,
                    SubmittedBy = dto.SubmittedBy
                };

                _repo.AddSuggestion(suggestion);
                return Ok(new { Message = "Feedback submitted successfully." });
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
    public class StatusUpdateRequest
    {
        public string NewStatus { get; set; }
        public string Notes { get; set; }
    }

    public class SubmitSuggestionDto
    {
        public string SuggestionType { get; set; }
        public string Description { get; set; }
        public int SubmittedBy { get; set; }
    }
}