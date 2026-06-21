using Logicc.Api.Entities;

namespace Logicc.Api.Data;

/// <summary>
/// In-memory data repository that simulates database operations without requiring a database connection.
/// </summary>
public class InMemoryProductRepository
{
    private static readonly List<Product> Products = new()
    {
        new Product { Id = Guid.Parse("8f14e45f-ceea-4d95-8b3d-000000000001"), Name = "Industrial Widget" },
        new Product { Id = Guid.Parse("8f14e45f-ceea-4d95-8b3d-000000000002"), Name = "Precision Gadget" },
        new Product { Id = Guid.Parse("8f14e45f-ceea-4d95-8b3d-000000000003"), Name = "Wireless Sensor Module" }
    };

    private static readonly object _lock = new object();

    /// <summary>
    /// Gets all products ordered by name.
    /// </summary>
    public Task<IEnumerable<Product>> GetAllAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(Products.OrderBy(p => p.Name).AsEnumerable());
        }
    }

    /// <summary>
    /// Gets a specific product by ID.
    /// </summary>
    public Task<Product?> GetByIdAsync(Guid id)
    {
        lock (_lock)
        {
            return Task.FromResult(Products.FirstOrDefault(p => p.Id == id));
        }
    }

    /// <summary>
    /// Adds a new product.
    /// </summary>
    public Task<Product> AddAsync(Product product)
    {
        lock (_lock)
        {
            Products.Add(product);
            return Task.FromResult(product);
        }
    }

    /// <summary>
    /// Updates an existing product.
    /// </summary>
    public Task<Product?> UpdateAsync(Guid id, Product updatedProduct)
    {
        lock (_lock)
        {
            var product = Products.FirstOrDefault(p => p.Id == id);
            if (product is null)
                return Task.FromResult<Product?>(null);

            product.Name = updatedProduct.Name;
            return Task.FromResult<Product?>(product);
        }
    }

    /// <summary>
    /// Deletes a product by ID.
    /// </summary>
    public Task<bool> DeleteAsync(Guid id)
    {
        lock (_lock)
        {
            var product = Products.FirstOrDefault(p => p.Id == id);
            if (product is null)
                return Task.FromResult(false);

            Products.Remove(product);
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Clears all products (useful for testing).
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            Products.Clear();
            // Re-seed with default data
            Products.AddRange(new[]
            {
                new Product { Id = Guid.Parse("8f14e45f-ceea-4d95-8b3d-000000000001"), Name = "Industrial Widget" },
                new Product { Id = Guid.Parse("8f14e45f-ceea-4d95-8b3d-000000000002"), Name = "Precision Gadget" },
                new Product { Id = Guid.Parse("8f14e45f-ceea-4d95-8b3d-000000000003"), Name = "Wireless Sensor Module" }
            });
        }
    }
}
