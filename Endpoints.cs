using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using StockTrack.Api.Data;
using StockTrack.Api.Models;
using StockTrack.Api.Services;

namespace StockTrack.Api;

public record LoginRequest(string Username, string Password, string? Portal, bool Remember = true);
public record NameRequest(string Name);
public record PasswordRequest(string Current, string Next);
public record NewStaffRequest(string Name, string Username, string Password, string Role = Roles.Staff);
public record ResetPasswordRequest(string NewPassword);
public record CategoryRequest(string Name);
public record TransactionRequest(string ProductId, string Type, int Qty, string? Note);

public static class Endpoints
{
    public static void MapApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        // ---------- Auth ----------
        var authGroup = api.MapGroup("/auth");

        authGroup.MapPost("/login", async (LoginRequest r, HttpContext ctx, AuthService auth) =>
        {
            var user = await auth.AttemptLoginAsync(r.Username.Trim(), r.Password, r.Portal ?? "staff");
            await auth.SignInAsync(ctx, user, r.Remember);
            return Results.Ok(UserDto.From(user));
        });

        authGroup.MapPost("/logout", async (HttpContext ctx, AuthService auth) =>
        {
            await auth.SignOutAsync(ctx);
            return Results.NoContent();
        });

        authGroup.MapGet("/me", (ClaimsPrincipal u) => Results.Ok(new
        {
            userId = u.FindFirstValue(ClaimTypes.NameIdentifier),
            username = u.Identity?.Name,
            name = u.FindFirstValue("display_name"),
            role = u.FindFirstValue(ClaimTypes.Role)
        })).RequireAuthorization("StaffOrAdmin");

        authGroup.MapPut("/me/name", async (NameRequest r, HttpContext ctx, CurrentUser me, UserService users, AuthService auth) =>
        {
            var updated = await users.UpdateNameAsync(me.Id!, r.Name);
            await auth.RefreshSessionAsync(ctx, updated);
            return Results.Ok(UserDto.From(updated));
        }).RequireAuthorization("AdminOnly");

        authGroup.MapPost("/me/password", async (PasswordRequest r, CurrentUser me, AuthService auth) =>
        {
            await auth.ChangeOwnPasswordAsync(me.Id!, r.Current, r.Next);
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        // ---------- Users (admin) ----------
        var usersGroup = api.MapGroup("/users").RequireAuthorization("AdminOnly");
        usersGroup.MapGet("/", async (UserService s) => (await s.ListAsync()).Select(UserDto.From));
        usersGroup.MapPost("/", async (NewStaffRequest r, UserService s) =>
            UserDto.From(await s.AddStaffAccountAsync(r.Name.Trim(), r.Username.Trim(), r.Password, r.Role)));
        usersGroup.MapPost("/{id}/reset-password", async (string id, ResetPasswordRequest r, UserService s) =>
        {
            if (string.IsNullOrWhiteSpace(r.NewPassword)) throw new AppException("Password cannot be empty");
            await s.ResetPasswordAsync(id, r.NewPassword.Trim());
            return Results.NoContent();
        });
        usersGroup.MapDelete("/{id}", async (string id, UserService s) => { await s.RemoveAsync(id); return Results.NoContent(); });

        // ---------- Products ----------
        var products = api.MapGroup("/products").RequireAuthorization("StaffOrAdmin");
        products.MapGet("/", (InventoryService s) => s.ListProductsAsync());
        products.MapGet("/low-stock", (int? limit, InventoryService s) => s.GetLowStockAsync(limit));
        products.MapGet("/{id}", async (string id, InventoryService s) =>
            await s.GetProductAsync(id) is { } p ? Results.Ok(p) : Results.NotFound(new { error = "Product not found." }));
        products.MapPost("/", async (ProductInput d, InventoryService s) => Results.Ok(await s.AddProductAsync(d)))
            .RequireAuthorization("AdminOnly");
        products.MapPut("/{id}", async (string id, ProductInput d, InventoryService s) =>
        {
            await s.UpdateProductAsync(id, d);
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");
        products.MapDelete("/{id}", async (string id, InventoryService s) =>
        {
            await s.DeleteProductAsync(id);
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        // ---------- Categories ----------
        var cats = api.MapGroup("/categories").RequireAuthorization("StaffOrAdmin");
        cats.MapGet("/", (InventoryService s) => s.ListCategoriesAsync());
        cats.MapPost("/", async (CategoryRequest r, InventoryService s) => { await s.AddCategoryAsync(r.Name); return Results.NoContent(); })
            .RequireAuthorization("AdminOnly");
        cats.MapPut("/{name}", async (string name, CategoryRequest r, InventoryService s) => { await s.UpdateCategoryAsync(name, r.Name); return Results.NoContent(); })
            .RequireAuthorization("AdminOnly");
        cats.MapDelete("/{name}", async (string name, InventoryService s) => Results.Ok(new { movedProducts = await s.DeleteCategoryAsync(name) }))
            .RequireAuthorization("AdminOnly");

        // ---------- Transactions (stock in / out) ----------
        var tx = api.MapGroup("/transactions").RequireAuthorization("StaffOrAdmin");
        tx.MapGet("/", (string? type, InventoryService s) => s.ListTransactionsAsync(type));
        tx.MapPost("/", async (TransactionRequest r, InventoryService s) =>
        {
            var t = await s.AddTransactionAsync(r.ProductId, r.Type, r.Qty, r.Note);
            return Results.Ok(t);
        });

        // ---------- KPIs / reports ----------
        api.MapGet("/kpis", (InventoryService s) => s.ComputeKpisAsync()).RequireAuthorization("StaffOrAdmin");

        // ---------- Activity log + demo reset (admin) ----------
        var activity = api.MapGroup("/activity").RequireAuthorization("AdminOnly");
        activity.MapGet("/", (ActivityService s) => s.ListAsync());
        activity.MapDelete("/", async (ActivityService s) => { await s.ClearAsync(); return Results.NoContent(); });

        api.MapPost("/admin/reset-demo-data", async (AppDb db, IPasswordHasher<User> hasher) =>
        {
            await Seeder.ResetAsync(db, hasher);
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");
    }
}
