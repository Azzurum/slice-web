using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient; // Required for SQL Connection
using Dapper;                   // Required for Dapper Queries
using SLICE_Website.Data;
using SLICE_Website.Models;
using SLICE_Website.Services;
using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration; // Required to read connection string

namespace SLICE_Website.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserRepository _userRepo;
        private readonly EmailService _emailService;
        private readonly AuditRepository _auditRepo;
        private readonly string _connectionString;

        // Injected IConfiguration so we can read the DB connection string safely
        public AuthController(UserRepository userRepo, EmailService emailService, AuditRepository auditRepo, IConfiguration config)
        {
            _userRepo = userRepo;
            _emailService = emailService;
            _auditRepo = auditRepo;

            // Grabs "DefaultConnection" from your appsettings.json. Make sure it exists!
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "YourDatabaseConnectionString";
        }

        // ========================================================
        // STANDARD LOGIN
        // ========================================================
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest req)
        {
            var user = _userRepo.Login(req.Username, req.Password);

            if (user != null)
            {
                return Ok(user);
            }

            return Unauthorized("Oops! Those credentials don't match our recipe.");
        }

        // ========================================================
        // MANAGER OVERRIDE (For POS Void Auth)
        // ========================================================
        [HttpPost("override")]
        public IActionResult ManagerOverride([FromBody] OverrideRequest req)
        {
            try
            {
                // Use your existing, working Login method which correctly handles password hashing!
                var user = _userRepo.Login(req.Username, req.Password);

                if (user != null)
                {
                    // Check if the valid user actually has Manager privileges
                    if (user.Role == "Manager" || user.Role == "Super-Admin" || user.Role == "Owner")
                    {
                        return Ok(user.UserID);
                    }
                }

                // If login fails, or they don't have the right role, reject them
                return Unauthorized("Invalid credentials or insufficient permissions.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // ========================================================
        // 1. SEND VERIFICATION CODE
        // ========================================================
        [HttpPost("forgot-password")]
        public IActionResult ForgotPassword([FromBody] ForgotPasswordDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Email)) return BadRequest("Email is required.");

            var user = _userRepo.GetUserByEmail(request.Email);
            if (user == null)
            {
                return BadRequest("Email not found in our system.");
            }

            // Generate 6-digit code
            Random rnd = new Random();
            string code = rnd.Next(100000, 999999).ToString();

            // Save to database with 15-minute expiration
            _userRepo.SaveResetCode(user.UserID, code, DateTime.UtcNow.AddMinutes(15));

            // Send Email
            bool emailSent = _emailService.SendPasswordResetEmail(request.Email, code);

            if (emailSent)
            {
                return Ok(new { Message = "Verification code sent." });
            }
            else
            {
                return StatusCode(500, "Failed to send email. Check SMTP settings.");
            }
        }

        // ========================================================
        // 2. VERIFY CODE
        // ========================================================
        [HttpPost("verify-code")]
        public IActionResult VerifyCode([FromBody] VerifyCodeDto request)
        {
            if (_userRepo.VerifyResetCode(request.Email, request.Code))
            {
                return Ok(new { Message = "Code verified successfully." });
            }
            else
            {
                return BadRequest("Invalid or expired code.");
            }
        }

        // ========================================================
        // 3. RESET PASSWORD & AUDIT LOG
        // ========================================================
        [HttpPost("reset-password")]
        public IActionResult ResetPassword([FromBody] ResetPasswordDto request)
        {
            // 1. Validate Enterprise Complexity
            var passwordRegex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$");
            if (!passwordRegex.IsMatch(request.NewPassword))
            {
                return BadRequest("Password must be at least 8 characters long and contain at least one uppercase letter, one lowercase letter, and one number.");
            }

            // 2. Get User ID for logging
            var user = _userRepo.GetUserByEmail(request.Email);
            if (user == null) return BadRequest("User not found.");

            // 3. Update the password
            _userRepo.UpdatePassword(request.Email, request.NewPassword);

            // 4. Log to Audit Trail
            _auditRepo.LogAction(user.UserID, "SECURITY", "User reset their password via email verification.");

            return Ok(new { Message = "Password successfully updated." });
        }

        // ========================================================
        // DATA TRANSFER OBJECTS (DTOs)
        // ========================================================
        public class LoginRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class OverrideRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class ForgotPasswordDto
        {
            public string Email { get; set; } = string.Empty;
        }

        public class VerifyCodeDto
        {
            public string Email { get; set; } = string.Empty;
            public string Code { get; set; } = string.Empty;
        }

        public class ResetPasswordDto
        {
            public string Email { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }
    }
}