using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StockTrack.Api;
using StockTrack.Api.Data;
using StockTrack.Api.Models;
using StockTrack.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Database (SQLite file next to the app) — replaces localStorage
builder.Services.AddDbContext<AppDb>(o => o.UseSqlite(
    builder.Configuration.GetConnectionString("Default") ?? "Data Source=stocktrack.db"));

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<ActivityService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<InventoryService>();

// Server-side session = auth cookie. API-style responses instead of redirects.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "stocktrack_session";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
        o.SlidingExpiration = true;
        // if the user was removed or their role changed, the old cookie stops working
        o.Events.OnValidatePrincipal = async c =>
        {
            var id = c.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = c.Principal?.FindFirstValue(ClaimTypes.Role);
            var db = c.HttpContext.RequestServices.GetRequiredService<AppDb>();
            var user = id == null ? null : await db.Users.FindAsync(id);
            if (user == null || user.Role != role)
            {
                c.RejectPrincipal();
                await c.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
        o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
    });

// requireAuth(['administrator'...]) from auth.js -> role policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", p => p.RequireRole(Roles.Administrator))
    .AddPolicy("StaffOrAdmin", p => p.RequireRole(Roles.Administrator, Roles.Staff));

var app = builder.Build();

// AppException -> { "error": "..." } with the right status code (like the thrown Errors in db.js)
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (AppException ex)
    {
        ctx.Response.StatusCode = ex.Status;
        await ctx.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

// create the database + seed demo data on first run (getDB()/seedDB())
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    await db.Database.EnsureCreatedAsync();
    await Seeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>());
}

// serve the frontend (wwwroot/index.html is the default page)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.MapApi();

app.Run();
