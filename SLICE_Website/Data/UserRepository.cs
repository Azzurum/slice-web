using System;
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

        // =========================================================
        // 1. AUTHENTICATION (REMOVED CRASHING COLUMNS)
        // =========================================================
        public User? Login(string username, string password)
        {
            using (var connection = _dbService.GetConnection())
            {
                // 1. ONLY search by Username. Do NOT check the password in SQL.
                string sql = @"
                    SELECT u.*, b.BranchName 
                    FROM Users u 
                    LEFT JOIN Branches b ON u.BranchID = b.BranchID 
                    WHERE u.Username = @Username AND u.IsActive = 1";

                var user = connection.QuerySingleOrDefault<User>(sql, new { Username = username });

                // 2. Verify the plain-text password against the stored BCrypt hash
                if (user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    return user; // Password is correct!
                }

                return null; // Username not found OR Password incorrect
            }
        }

        // =========================================================
        // 2. RETRIEVE USERS
        // =========================================================
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

        public void UpdateUser(User user)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = @"
                    UPDATE Users 
                    SET Username = @Username, PasswordHash = @PasswordHash, FullName = @FullName, 
                        Email = @Email, Role = @Role, BranchID = @BranchID
                    WHERE UserID = @UserID";
                connection.Execute(sql, user);
            }
        }

        public void DeactivateUser(int userId)
        {
            using (var connection = _dbService.GetConnection())
            {
                connection.Execute("UPDATE Users SET IsActive = 0 WHERE UserID = @UserID", new { UserID = userId });
            }
        }

        public void ReactivateUser(int userId)
        {
            using (var connection = _dbService.GetConnection())
            {
                connection.Execute("UPDATE Users SET IsActive = 1 WHERE UserID = @UserID", new { UserID = userId });
            }
        }

        public User? GetUserByEmail(string email)
        {
            using (var connection = _dbService.GetConnection())
            {
                return connection.QuerySingleOrDefault<User>("SELECT * FROM Users WHERE Email = @Email AND IsActive = 1", new { Email = email });
            }
        }

        public void SaveResetCode(int userId, string code, DateTime expiry)
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
                string sql = "SELECT COUNT(1) FROM Users WHERE Email = @Email AND ResetCode = @Code AND ResetCodeExpiry > GETUTCDATE()";
                return connection.ExecuteScalar<int>(sql, new { Email = email, Code = code }) > 0;
            }
        }

        public void UpdatePassword(string email, string newPassword)
        {
            using (var connection = _dbService.GetConnection())
            {
                string sql = "UPDATE Users SET PasswordHash = @Password, ResetCode = NULL, ResetCodeExpiry = NULL WHERE Email = @Email";
                connection.Execute(sql, new { Password = newPassword, Email = email });
            }
        }
    }
}