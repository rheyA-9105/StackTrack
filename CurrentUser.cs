using System.Security.Claims;

namespace StockTrack.Api.Services;

// Server-side replacement for getSession(): reads who is logged in from the auth cookie.
public class CurrentUser(IHttpContextAccessor http)
{
    public ClaimsPrincipal? Principal => http.HttpContext?.User;
    public string? Id => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? Role => Principal?.FindFirstValue(ClaimTypes.Role);
}
