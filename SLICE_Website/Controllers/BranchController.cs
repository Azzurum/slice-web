using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SLICE_Website.Data;
using SLICE_Website.Models;
using System.Collections.Generic;
using System.Linq;
using Dapper;

namespace SLICE_Website.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BranchController : ControllerBase
    {
        private readonly IConfiguration _config;

        public BranchController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet]
        public IActionResult GetBranches()
        {
            try
            {
                using (var conn = new DatabaseService(_config).GetConnection())
                {
                    var list = conn.Query<Branch>("SELECT * FROM Branches ORDER BY BranchID ASC").ToList();
                    return Ok(list);
                }
            }
            catch (System.Exception ex) { return StatusCode(500, ex.Message); }
        }

        [HttpPost]
        public IActionResult AddBranch([FromBody] Branch branch)
        {
            try
            {
                using (var conn = new DatabaseService(_config).GetConnection())
                {
                    string sql = "INSERT INTO Branches (BranchName, Location, ContactNumber) VALUES (@BranchName, @Location, @ContactNumber)";
                    conn.Execute(sql, branch);
                    return Ok();
                }
            }
            catch (System.Exception ex) { return StatusCode(500, ex.Message); }
        }

        [HttpPut("{id}")]
        public IActionResult UpdateBranch(int id, [FromBody] Branch branch)
        {
            try
            {
                using (var conn = new DatabaseService(_config).GetConnection())
                {
                    string sql = "UPDATE Branches SET BranchName = @BranchName, Location = @Location, ContactNumber = @ContactNumber WHERE BranchID = @Id";
                    conn.Execute(sql, new { branch.BranchName, branch.Location, branch.ContactNumber, Id = id });
                    return Ok();
                }
            }
            catch (System.Exception ex) { return StatusCode(500, ex.Message); }
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteBranch(int id)
        {
            try
            {
                using (var conn = new DatabaseService(_config).GetConnection())
                {
                    conn.Execute("DELETE FROM Branches WHERE BranchID = @Id", new { Id = id });
                    return Ok();
                }
            }
            catch (System.Exception)
            {
                return BadRequest("Cannot delete this branch because it currently has active inventory or sales records linked to it.");
            }
        }
    }
}