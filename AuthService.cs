using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using StockTrack.Api.Models;

namespace StockTrack.Api.Services;

// Backend half of auth.js only.
// Left on the frontend (not converted): sessionStorage/localStorage handling,
// window.location redirects, ROLE_LABELS (display text).
public class AuthService(UserService users, ActivityService activity, IPasswordHasher<User> hasher)
{
    // attemptLogin(): check credentials + portal access
    public async Task<User> AttemptLoginAsync(string username, string password, string portal)
    {
        var user = await users.FindByUsernameAsync(username);
        if (user == null ||
            hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
            throw new AppException("Incorrect username or password.", 401);

        var allowed = user.Role == Roles.Administrator || user.Role == portal;
        if (!allowed) throw new AppException("This account does not have access to the Staff Portal.", 403);

        await activity.LogAsync($"\"{user.Username}\" logged in via {portal} portal", user.Id);
        return user;
    }

    // setSession(): the auth cookie is the server-side session.
    // remember=true -> persistent cookie (was localStorage); false -> session cookie (was sessionStorage)
    public Task SignInAsync(HttpContext ctx, User user, bool remember)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Username),
            new("display_name", user.Name),
            new(ClaimTypes.Role, user.Role),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        return ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = remember });
    }

    // updateSessionName(): re-issue the cookie with fresh claims, keeping persistence
    public async Task RefreshSessionAsync(HttpContext ctx, User user)
    {
        var result = await ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await SignInAsync(ctx, user, result.Properties?.IsPersistent ?? false);
    }

    // logout() / clearSession()
    public async Task SignOutAsync(HttpContext ctx)
    {
        var username = ctx.User.Identity?.IsAuthenticated == true ? ctx.User.Identity.Name : null;
        if (username != null) await activity.LogAsync($"\"{username}\" logged out");
        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    // settings.html compared the current password in the browser; the server does it now
    public async Task ChangeOwnPasswordAsync(string userId, string current, string next)
    {
        var user = await users.GetByIdAsync(userId) ?? throw new AppException("User not found.", 404);
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, current) == PasswordVerificationResult.Failed)
            throw new AppException("Current password is incorrect");
        if (string.IsNullOrEmpty(next) || next.Length < 4) throw new AppException("New password must be at least 4 characters.");
        await users.ResetPasswordAsync(userId, next, mustReset: false);
    }

    // requireAuth(allowedRoles) is now the "AdminOnly" / "StaffOrAdmin" policies in Program.cs
}
