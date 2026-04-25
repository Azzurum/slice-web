using Microsoft.AspNetCore.Mvc;
using SLICE_Website.Data;
using SLICE_Website.Models;
using Dapper;
using System.Linq;

namespace SLICE_Website.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly UserRepository _repo;
        private readonly DatabaseService _dbService;

        // Injected both repo and config to allow the quick branches query
        public UserController(UserRepository repo, Microsoft.Extensions.Configuration.IConfiguration config)
        {
            _repo = repo;
            _dbService = new DatabaseService(config);
        }

        // --- 1. GET ALL USERS ---
        [HttpGet]
        public IActionResult GetAllUsers([FromQuery] string search = "")
        {
            try
            {
                var users = _repo.GetAllUsers(search ?? "");
                return Ok(users);
            }
            catch (System.Exception ex) { return StatusCode(500, "Database Error: " + ex.Message); }
        }

        // --- 2. ADD USER ---
        [HttpPost]
        public IActionResult AddUser([FromBody] User user)
        {
            try
            {
                // HASH THE PASSWORD BEFORE SAVING!
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);

                _repo.AddUser(user);
                return Ok(new { Message = "User added successfully." });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, "Database Error: " + ex.Message);
            }
        }

        // --- 3. EDIT USER ---
        [HttpPut("{id}")]
        public IActionResult UpdateUser(int id, [FromBody] User user)
        {
            try
            {
                user.UserID = id;

                // SECURITY FIX: Hash the password ONLY if it is a new plain-text password.
                // If it doesn't start with "$2", it is plain text. 
                // If it does start with "$2", it's the old hash being passed back, so we leave it alone.
                if (!string.IsNullOrWhiteSpace(user.PasswordHash) && !user.PasswordHash.StartsWith("$2"))
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
                }

                _repo.UpdateUser(user);
                return Ok(new { Message = "User updated successfully." });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, "Database Error: " + ex.Message);
            }
        }

        // --- 4. DEACTIVATE USER ---
        [HttpPut("{id}/deactivate")]
        public IActionResult DeactivateUser(int id)
        {
            try
            {
                _repo.DeactivateUser(id);
                return Ok(new { Message = "User deactivated successfully." });
            }
            catch (System.Exception ex) { return StatusCode(500, "Database Error: " + ex.Message); }
        }

        // --- 5. RESTORE USER ---
        [HttpPut("{id}/restore")]
        public IActionResult RestoreUser(int id)
        {
            try
            {
                _repo.ReactivateUser(id);
                return Ok(new { Message = "User restored successfully." });
            }
            catch (System.Exception ex) { return StatusCode(500, "Database Error: " + ex.Message); }
        }

        // --- 6. GET BRANCHES FOR DROPDOWN (No Simulation) ---
        [HttpGet("branches")]
        public IActionResult GetBranches()
        {
            try
            {
                using (var conn = _dbService.GetConnection())
                {
                    var branches = conn.Query("SELECT BranchID, BranchName FROM Branches").ToList();
                    return Ok(branches);
                }
            }
            catch (System.Exception ex) { return StatusCode(500, "Database Error: " + ex.Message); }
        }
    }
}