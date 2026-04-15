using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration; // ADD THIS for reading appsettings.json

namespace SLICE_Website.Data // Make sure this matches your new API project name!
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        // 1. CONSTRUCTOR INJECTION
        // ASP.NET Core will automatically pass the IConfiguration object here
        public DatabaseService(IConfiguration configuration)
        {
            // 2. READ FROM APPSETTINGS.JSON
            // This looks for "SliceDbConnection" inside the "ConnectionStrings" block
            _connectionString = configuration.GetConnectionString("SliceDbConnection");
        }

        // 3. METHOD TO GET A CONNECTION
        public IDbConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        // 4. THE SMOKE TEST METHOD
        public bool TestConnection()
        {
            try
            {
                using (var connection = GetConnection())
                {
                    connection.Open();
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Note: Debug.WriteLine is usually replaced by ILogger in Web APIs, 
                // but Console.WriteLine works for basic testing.
                Console.WriteLine("Connection Error: " + ex.Message);
                return false;
            }
        }
    }
}