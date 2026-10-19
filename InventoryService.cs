using Microsoft.EntityFrameworkCore;
using StockTrack.Api.Data;
using StockTrack.Api.Models;

namespace StockTrack.Api.Services;

// Products, Categories, Transactions and KPIs sections of db.js
public class InventoryService(AppDb db, ActivityService activity, CurrentUser me)
{
    // ---------- Products ----------
    public Task<List<Product>> ListProductsAsync() => db.Products.OrderBy(p => p.Name).ToListAsync();

    public Task<Product?> GetProductAsync(string id) => db.Products.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Product> AddProductAsync(ProductInput d)
    {
        if (string.IsNullOrWhiteSpace(d.Name)) throw new AppException("Product name is required.");
        var p = new Product
        {
            Id = Ids.New("p"), Name = d.Name.Trim(), Category = d.Category.Trim(), Supplier = d.Supplier.Trim(),
            Stock = d.Stock, MinStock = d.MinStock, Price = d.Price
        };
        db.Products.Add(p);
        await db.SaveChangesAsync();
        await activity.LogAsync($"Added product \"{p.Name}\"");
        return p;
    }

    public async Task UpdateProductAsync(string id, ProductInput d)
    {
        var p = await GetProductAsync(id) ?? throw new AppException("Product not found.", 404);
        p.Name = d.Name.Trim(); p.Category = d.Category.Trim(); p.Supplier = d.Supplier.Trim();
        p.Stock = d.Stock; p.MinStock = d.MinStock; p.Price = d.Price;
        await db.SaveChangesAsync();
        await activity.LogAsync($"Edited product \"{p.Name}\"");
    }

    public async Task DeleteProductAsync(string id)
    {
        var p = await GetProductAsync(id);
        if (p == null) return;
        db.Products.Remove(p);
        await db.SaveChangesAsync();
        await activity.LogAsync($"Deleted product \"{p.Name}\"");
    }

    // ---------- Categories ----------
    public async Task<List<string>> ListCategoriesAsync()
    {
        var stored = await db.Categories.Select(c => c.Name).ToListAsync();
        var fromProducts = await db.Products.Select(p => p.Category).ToListAsync();
        return stored.Union(fromProducts)
            .Where(c => c != "Uncategorized" || fromProducts.Contains(c))
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
    }

    public async Task AddCategoryAsync(string? name)
    {
        var category = (name ?? "").Trim();
        if (category.Length == 0) throw new AppException("Category name cannot be empty.");
        if ((await ListCategoriesAsync()).Any(c => c.Equals(category, StringComparison.OrdinalIgnoreCase)))
            throw new AppException("That category already exists.");
        db.Categories.Add(new Category { Name = category });
        await db.SaveChangesAsync();
        await activity.LogAsync($"Created category \"{category}\"");
    }

    public async Task UpdateCategoryAsync(string name, string? nextName)
    {
        var current = name.Trim();
        var category = (nextName ?? "").Trim();
        if (category.Length == 0) throw new AppException("Category name cannot be empty.");
        if (!current.Equals(category, StringComparison.OrdinalIgnoreCase) &&
            (await ListCategoriesAsync()).Any(c => c.Equals(category, StringComparison.OrdinalIgnoreCase)))
            throw new AppException("That category already exists.");

        foreach (var c in await db.Categories.Where(c => c.Name == current).ToListAsync()) c.Name = category;
        foreach (var p in await db.Products.Where(p => p.Category == current).ToListAsync()) p.Category = category;
        await db.SaveChangesAsync();
        await activity.LogAsync($"Renamed category \"{current}\" to \"{category}\"");
    }

    public async Task<int> DeleteCategoryAsync(string name)
    {
        var category = name.Trim();
        var toMove = await db.Products.Where(p => p.Category == category).ToListAsync();
        if (toMove.Count > 0)
        {
            foreach (var p in toMove) p.Category = "Uncategorized";
            if (!await db.Categories.AnyAsync(c => c.Name == "Uncategorized"))
                db.Categories.Add(new Category { Name = "Uncategorized" });
        }
        db.Categories.RemoveRange(await db.Categories.Where(c => c.Name == category).ToListAsync());
        await db.SaveChangesAsync();
        await activity.LogAsync($"Deleted category \"{category}\"" +
            (toMove.Count > 0 ? $" and moved {toMove.Count} product(s) to Uncategorized" : ""));
        return toMove.Count;
    }

    // ---------- Transactions (stock in / out) ----------
    public async Task<StockTransaction> AddTransactionAsync(string productId, string type, int qty, string? note)
    {
        var product = await GetProductAsync(productId) ?? throw new AppException("Product not found.", 404);
        qty = Math.Abs(qty);
        if (qty <= 0) throw new AppException("Quantity must be greater than zero.");
        if (type != "IN" && type != "OUT") throw new AppException("Type must be IN or OUT.");
        if (type == "OUT" && qty > product.Stock)
            throw new AppException($"Cannot remove {qty} units — only {product.Stock} in stock.");

        product.Stock += type == "IN" ? qty : -qty;
        var tx = new StockTransaction
        {
            Id = Ids.New("t"), ProductId = productId, Type = type, Qty = qty,
            UserId = me.Id, Note = note ?? "", At = DateTime.UtcNow
        };
        db.Transactions.Add(tx);
        await db.SaveChangesAsync();   // stock change + transaction saved together
        await activity.LogAsync($"{(type == "IN" ? "Added" : "Removed")} {qty} unit(s) of \"{product.Name}\"");
        return tx;
    }

    public async Task<List<TransactionDto>> ListTransactionsAsync(string? type = null)
    {
        var q = db.Transactions.AsQueryable();
        if (type is "IN" or "OUT") q = q.Where(t => t.Type == type);
        var txs = await q.OrderByDescending(t => t.At).ToListAsync();
        var products = await db.Products.ToDictionaryAsync(p => p.Id, p => p.Name);
        var users = await db.Users.ToDictionaryAsync(u => u.Id, u => u.Name);
        return txs.Select(t => new TransactionDto(
            t.Id, t.ProductId,
            products.GetValueOrDefault(t.ProductId),
            t.Type, t.Qty, t.UserId,
            t.UserId != null ? users.GetValueOrDefault(t.UserId) : null,
            t.Note, t.At)).ToList();
    }

    // ---------- KPIs ----------
    public async Task<KpisDto> ComputeKpisAsync()
    {
        var products = await db.Products.ToListAsync();
        return new KpisDto(
            products.Count,
            products.Sum(p => p.Stock),
            products.Count(p => p.Stock > 0 && p.Stock <= p.MinStock),
            products.Count(p => p.Stock == 0),
            products.Select(p => p.Category).Distinct().Count(),
            products.Select(p => p.Supplier).Distinct().Count());
    }

    public async Task<List<Product>> GetLowStockAsync(int? limit = null)
    {
        var list = await db.Products.Where(p => p.Stock <= p.MinStock).OrderBy(p => p.Stock).ToListAsync();
        return limit is > 0 ? list.Take(limit.Value).ToList() : list;
    }
}
