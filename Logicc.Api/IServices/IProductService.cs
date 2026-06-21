using Logicc.Api.Models;

namespace Logicc.Api.IServices;

/// <summary>
/// Service interface for managing product operations.
/// </summary>
public interface IProductService
{
    /// <summary>
    /// Gets all products ordered by name.
    /// </summary>
    Task<IEnumerable<ProductDto>> GetAllProductsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets a specific product by ID.
    /// </summary>
    Task<ProductDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new product with the provided data.
    /// </summary>
    Task<ProductDto> CreateProductAsync(string name, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing product's name.
    /// </summary>
    Task<ProductDto?> UpdateProductAsync(Guid id, string name, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a product by ID.
    /// </summary>
    Task<bool> DeleteProductAsync(Guid id, CancellationToken cancellationToken);
}
