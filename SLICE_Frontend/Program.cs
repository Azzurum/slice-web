using SLICE_Frontend.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SLICE_Frontend.Auth;

var builder = WebApplication.CreateBuilder(args);

// 1. Services - External API Connection
// Keep ONLY THIS ONE registration, pointing to the secure https port!
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://localhost:7092/")
});

// 2. Blazor Framework Services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 3. --- AUTHENTICATION REGISTRATIONS ---
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/login";
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
// ---------------------------------------

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// --- THE CRITICAL ORDER ---
app.UseAuthentication();
app.UseAuthorization();
// --------------------------

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();