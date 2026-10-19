using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StockTrack.Api.Data;
using StockTrack.Api.Models;

namespace StockTrack.Api.Services;

// "Users" section of db.js
public class UserService(AppDb db, ActivityService activity, IPasswordHasher<User> hasher)
{
    public Task<User?> FindByUsernameAsync(string username) =>
        db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

    public Task<User?> GetByIdAsync(string id) => db.Users.FirstOrDefaultAsync(u => u.Id == id);

    public Task<List<User>> ListAsync() => db.Users.OrderBy(u => u.CreatedAt).ToListAsync();

    public async Task<User> AddStaffAccountAsync(string name, string username, string password, string role)
    {
        if (role != Roles.Staff) throw new AppException("Only Staff accounts can be created here.");
        if (await FindByUsernameAsync(username) != null) throw new AppException("That username already exists.");

        var user = new User { Id = Ids.New("u"), Username = username, Name = name, Role = role, CreatedAt = DateTime.UtcNow };
        user.PasswordHash = hasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        await activity.LogAsync($"Created new {role} account \"{username}\"");
        return user;
    }

    public async Task ResetPasswordAsync(string userId, string newPassword, bool mustReset = true)
    {
        var user = await GetByIdAsync(userId) ?? throw new AppException("User not found.", 404);
        user.PasswordHash = hasher.HashPassword(user, newPassword);
        user.MustReset = mustReset;
        await db.SaveChangesAsync();
        await activity.LogAsync($"Reset password for \"{user.Username}\"");
    }

    public async Task RemoveAsync(string userId)
    {
        var user = await GetByIdAsync(userId);
        if (user == null) return;
        if (user.Role == Roles.Administrator) throw new AppException("Administrator accounts cannot be removed.");
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        await activity.LogAsync($"Removed user \"{user.Username}\"");
    }

    public async Task<User> UpdateNameAsync(string userId, string? name)
    {
        var next = (name ?? "").Trim();
        if (next.Length == 0) throw new AppException("Name cannot be empty.");
        var user = await GetByIdAsync(userId) ?? throw new AppException("User not found.", 404);
        user.Name = next;
        await db.SaveChangesAsync();
        await activity.LogAsync($"Updated account name for \"{user.Username}\"");
        return user;
    }
}
