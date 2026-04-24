using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;

namespace SLICE_Frontend.Auth
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private ClaimsPrincipal _currentUser = new ClaimsPrincipal(new ClaimsIdentity());

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return Task.FromResult(new AuthenticationState(_currentUser));
        }

        public void LogIn(AuthUser user)
        {
            // We use the fully qualified name "System.Security.Claims.Claim" 
            // to stop the "BinaryReader" conversion error.
            var claims = new[]
            {
                new System.Security.Claims.Claim(ClaimTypes.Name, user.FullName ?? user.Username ?? "User"),
                new System.Security.Claims.Claim(ClaimTypes.Role, user.Role ?? "Guest"),
                new System.Security.Claims.Claim("UserID", user.UserID.ToString()),
                new System.Security.Claims.Claim("BranchID", user.BranchID?.ToString() ?? "0"),
                new System.Security.Claims.Claim("BranchName", user.BranchName ?? "Unknown Branch"),
                new System.Security.Claims.Claim("BranchAddress", user.BranchAddress ?? "Address Not Set"),
                new System.Security.Claims.Claim("BranchContact", user.BranchContact ?? "Contact Not Set")
            };

            var identity = new ClaimsIdentity(claims, "CustomAuth");
            _currentUser = new ClaimsPrincipal(identity);

            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        public void LogOut()
        {
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }

    // UPDATED MODEL: Added the missing fields so the compiler can find them
    public class AuthUser
    {
        public int UserID { get; set; }
        public string? Username { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int? BranchID { get; set; }
        public string? BranchName { get; set; }
        public string? BranchAddress { get; set; }
        public string? BranchContact { get; set; }
    }
}