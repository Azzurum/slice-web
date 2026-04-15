using System.Collections.Generic;
using System.Linq;
using Dapper;
using SLICE_Website.Models;

namespace SLICE_Website.Data
{
    public class UserRepository
    {
        private readonly DatabaseService _dbService;

        public UserRepository(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        // --- 1. AUTHENTICATION ---
        // Validates user credentials and ensures the account is actively permitted to log in
        public User? Login(string username, string password)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = "SELECT * FROM Users WHERE Username = @Username AND PasswordHash = @Password AND IsActive = 1";
                return connection.QuerySingleOrDefault<User>(sql, new { Username = username, Password = password });
            }
        }

        // --- 2. RETRIEVE USERS ---
        // Fetches all users, joins their assigned branch name, and applies an optional search filter
        public List<User> GetAllUsers(string search = "")
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    SELECT u.*, ISNULL(b.BranchName, 'Headquarters') as BranchName 
                    FROM Users u
                    LEFT JOIN Branches b ON u.BranchID = b.BranchID
                    WHERE (@Search = '' OR u.FullName LIKE @Search OR u.Username LIKE @Search)
                    ORDER BY u.Role, u.FullName";

                return connection.Query<User>(sql, new { Search = "%" + search + "%" }).AsList();
            }
        }

        // --- 3. CREATE USER (UPDATED TO INCLUDE EMAIL) ---
        // Inserts a newly registered employee into the system (defaults to Active = 1)
        public void AddUser(User user)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    INSERT INTO Users (Username, PasswordHash, FullName, Email, Role, BranchID, IsActive)
                    VALUES (@Username, @PasswordHash, @FullName, @Email, @Role, @BranchID, 1)";

                connection.Execute(sql, user);
            }
        }

        // --- 4. UPDATE USER (UPDATED TO INCLUDE EMAIL) ---
        // Modifies an existing employee's details, role, branch assignment, password, or email
        public void UpdateUser(User user)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    UPDATE Users 
                    SET Username = @Username, 
                        PasswordHash = @PasswordHash, 
                        FullName = @FullName, 
                        Email = @Email,
                        Role = @Role, 
                        BranchID = @BranchID
                    WHERE UserID = @UserID";

                connection.Execute(sql, user);
            }
        }

        // --- 5. DEACTIVATE USER (SOFT DELETE) ---
        // Revokes access immediately without deleting the row, keeping financial/audit logs perfectly intact
        public void DeactivateUser(int userId)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = "UPDATE Users SET IsActive = 0 WHERE UserID = @UserID";
                connection.Execute(sql, new { UserID = userId });
            }
        }

        // --- 6. REACTIVATE USER ---
        // Restores system access for a previously deactivated employee
        public void ReactivateUser(int userId)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = "UPDATE Users SET IsActive = 1 WHERE UserID = @UserID";
                connection.Execute(sql, new { UserID = userId });
            }
        }

        // ==========================================
        // --- 7. FORGOT PASSWORD METHODS ---
        // ==========================================

        public User? GetUserByEmail(string email)
        {
            using (var connection = _dbService.GetConnection())
            {
                return connection.QuerySingleOrDefault<User>("SELECT * FROM Users WHERE Email = @Email AND IsActive = 1", new { Email = email });
            }
        }

        public void SaveResetCode(int userId, string code, System.DateTime expiry)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = "UPDATE Users SET ResetCode = @Code, ResetCodeExpiry = @Expiry WHERE UserID = @UserID";
                connection.Execute(sql, new { Code = code, Expiry = expiry, UserID = userId });
            }
        }

        public bool VerifyResetCode(string email, string code)
        {
            using (var connection = _dbService.GetConnection())
            {
                // Change GETDATE() to GETUTCDATE() to match the C# change
                string sql = "SELECT COUNT(1) FROM Users WHERE Email = @Email AND ResetCode = @Code AND ResetCodeExpiry > GETUTCDATE()";
                return connection.ExecuteScalar<int>(sql, new { Email = email, Code = code }) > 0;
            }
        }

        public void UpdatePassword(string email, string newPassword)
        {
            using (var connection = _dbService.GetConnection())
            {
                // Clears the reset code after successful password change for security
                string sql = "UPDATE Users SET PasswordHash = @Password, ResetCode = NULL, ResetCodeExpiry = NULL WHERE Email = @Email";
                connection.Execute(sql, new { Password = newPassword, Email = email });
            }
        }
    }
}