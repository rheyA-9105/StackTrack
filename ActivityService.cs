using Microsoft.EntityFrameworkCore;
using StockTrack.Api.Data;
using StockTrack.Api.Models;

namespace StockTrack.Api.Services;

// logActivity() from db.js
public class ActivityService(AppDb db, CurrentUser me)
{
    public async Task LogAsync(string message, string? userId = null)
    {
        db.Activity.Add(new ActivityLog { Id = Ids.New("a"), Message = message, UserId = userId ?? me.Id, At = DateTime.UtcNow });
        await db.SaveChangesAsync();

        // keep only the latest 200 entries, like the original
        var stale = await db.Activity.OrderByDescending(a => a.At).Skip(200).ToListAsync();
        if (stale.Count > 0)
        {
            db.Activity.RemoveRange(stale);
            await db.SaveChangesAsync();
        }
    }

    public async Task<List<ActivityDto>> ListAsync()
    {
        var logs = await db.Activity.OrderByDescending(a => a.At).ToListAsync();
        var users = await db.Users.ToDictionaryAsync(u => u.Id, u => u.Name);
        return logs.Select(l => new ActivityDto(l.Id,
            l.UserId != null && users.TryGetValue(l.UserId, out var n) ? n : "System",
            l.Message, l.At)).ToList();
    }

    public async Task ClearAsync() => await db.Activity.ExecuteDeleteAsync();
}
