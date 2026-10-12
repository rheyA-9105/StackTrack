using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StockTrack.Api.Models;

namespace StockTrack.Api.Data;

// Port of seedDB() / resetDB() from db.js
public static class Seeder
{
    public static async Task SeedAsync(AppDb db, IPasswordHasher<User> hasher)
    {
        if (await db.Users.AnyAsync()) return;
        var now = DateTime.UtcNow;

        User U(string id, string username, string pwd, string role, string name, int daysAgo)
        {
            var u = new User { Id = id, Username = username, Role = role, Name = name, CreatedAt = now.AddDays(-daysAgo) };
            u.PasswordHash = hasher.HashPassword(u, pwd);
            return u;
        }
        static Product P(string id, string name, string cat, string sup, int stock, int min, decimal price) =>
            new() { Id = id, Name = name, Category = cat, Supplier = sup, Stock = stock, MinStock = min, Price = price };
        StockTransaction T(string id, string pid, string type, int qty, string uid, DateTime at) =>
            new() { Id = id, ProductId = pid, Type = type, Qty = qty, UserId = uid, At = at };

        db.Users.AddRange(
            U("u1", "admin",  "admin123", Roles.Administrator, "John Smith",    90),
            U("u2", "staff1", "staff123", Roles.Staff,         "Alice Rogers",  60),
            U("u3", "staff2", "staff123", Roles.Staff,         "Brad Peterson", 45));

        db.Products.AddRange(
            P("p1",  "Wireless Mechanical Keyboard",     "Office IT",   "TechSource Co.",  4,  10, 45.00m),
            P("p2",  "Premium A4 Copier Paper (Box)",    "Stationery",  "PaperPlus Ltd.",  0,  15, 32.50m),
            P("p3",  "Black Ink Cartridge HP-962",       "Office IT",   "TechSource Co.",  2,  8,  18.75m),
            P("p4",  "Super Glue Adhesive Tube",         "Lab Utility", "LabWorks Supply", 5,  12, 3.20m),
            P("p5",  "Disposable Surgical Masks (50pk)", "Medical",     "MedCare Direct",  0,  20, 12.00m),
            P("p6",  "G2 Gel Ink Pens (Blue, 12pk)",     "Stationery",  "PaperPlus Ltd.",  60, 20, 8.40m),
            P("p7",  "A4 Paper Reams Copier Box",        "Stationery",  "PaperPlus Ltd.",  35, 15, 32.50m),
            P("p8",  "Premium Cardstock Heavyweight",    "Stationery",  "PaperPlus Ltd.",  48, 10, 14.00m),
            P("p9",  "Scientific Calculators Casio",     "Office IT",   "TechSource Co.",  22, 10, 16.99m),
            P("p10", "Whiteboard Dry Erase Markers",     "Stationery",  "PaperPlus Ltd.",  75, 20, 6.50m),
            P("p11", "Nitrile Lab Gloves (100pk)",       "Lab Utility", "LabWorks Supply", 18, 15, 9.99m),
            P("p12", "USB-C Charging Cable 2m",          "Office IT",   "TechSource Co.",  30, 10, 7.25m));

        db.Transactions.AddRange(
            T("t1", "p6",  "IN",  25, "u1", now.AddHours(-2)),
            T("t2", "p2",  "OUT", 5,  "u2", now.AddHours(-3.5)),
            T("t3", "p8",  "IN",  10, "u1", now.AddHours(-4.2)),
            T("t4", "p9",  "OUT", 12, "u3", now.AddDays(-1)),
            T("t5", "p10", "IN",  50, "u1", now.AddDays(-1).AddHours(-3)));

        db.Activity.Add(new ActivityLog { Id = "a1", Message = "System initialized with demo data", UserId = "u1", At = now.AddDays(-90) });
        await db.SaveChangesAsync();
    }

    public static async Task ResetAsync(AppDb db, IPasswordHasher<User> hasher)
    {
        await db.Transactions.ExecuteDeleteAsync();
        await db.Activity.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
        await db.Categories.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
        await SeedAsync(db, hasher);
    }
}
