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
            // Fully qualifying the Claim namespace to prevent the BinaryReader error
            var claims = new[]
            {
                new System.Security.Claims.Claim(ClaimTypes.Name, user.FullName ?? "User"),
                new System.Security.Claims.Claim(ClaimTypes.Role, user.Role ?? "Guest"),
                new System.Security.Claims.Claim("BranchID", user.BranchID?.ToString() ?? "0"),
                new System.Security.Claims.Claim("UserID", user.UserID.ToString())
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

    // A lightweight frontend version of the User model to catch the API data
    public class AuthUser
    {
        public int UserID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int? BranchID { get; set; }
    }
}