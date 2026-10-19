namespace StockTrack.Api.Models;

public static class Roles
{
    public const string Administrator = "administrator";
    public const string Staff = "staff";
}

public static class Ids
{
    // port of uid(prefix) from db.js
    public static string New(string prefix) => prefix + "_" + Guid.NewGuid().ToString("N")[..7];
}

public class AppException(string message, int status = 400) : Exception(message)
{
    public int Status { get; } = status;
}

public class User
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";   // hashed, never plain text
    public string Role { get; set; } = Roles.Staff;
    public string Name { get; set; } = "";
    public bool MustReset { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Product
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Supplier { get; set; } = "";
    public int Stock { get; set; }
    public int MinStock { get; set; }
    public decimal Price { get; set; }
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class StockTransaction
{
    public string Id { get; set; } = "";
    public string ProductId { get; set; } = "";
    public string Type { get; set; } = "IN";          // "IN" | "OUT"
    public int Qty { get; set; }
    public string? UserId { get; set; }
    public string Note { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public class ActivityLog
{
    public string Id { get; set; } = "";
    public string Message { get; set; } = "";
    public string? UserId { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}

// ---- DTOs (what the API sends / receives) ----
public record UserDto(string Id, string Username, string Name, string Role, DateTime CreatedAt)
{
    public static UserDto From(User u) => new(u.Id, u.Username, u.Name, u.Role, u.CreatedAt);
}
public record TransactionDto(string Id, string ProductId, string? ProductName, string Type, int Qty,
                             string? UserId, string? UserName, string Note, DateTime At);
public record ActivityDto(string Id, string Actor, string Message, DateTime At);
public record KpisDto(int TotalProducts, int TotalStock, int LowStock, int OutOfStock, int Categories, int Suppliers);
public record ProductInput(string Name, string Category, string Supplier, int Stock, int MinStock, decimal Price);
