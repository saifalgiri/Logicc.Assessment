using Logicc.Api.Data;
using Logicc.Api.Entities;
using Logicc.Api.IServices;
using Logicc.Api.Models;
using Logicc.AuditLogLib.IServices;

namespace Logicc.Api.Services;

/// <summary>
/// Service implementation for managing product operations using in-memory data storage.
/// </summary>
public class ProductService : IProductService
{
    private readonly InMemoryProductRepository _repository;
    private readonly IAdminLogService _auditLogService;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        InMemoryProductRepository repository,
        IAdminLogService auditLogService,
        ILogger<ProductService> logger)
    {
        _repository = repository;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<IEnumerable<ProductDto>> GetAllProductsAsync(CancellationToken cancellationToken)
    {
        var products = await _repository.GetAllAsync();
        return products.Select(ProductDto.FromEntity);
    }

    public async Task<ProductDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await _repository.GetByIdAsync(id);
        return product is null ? null : ProductDto.FromEntity(product);
    }

    public async Task<ProductDto> CreateProductAsync(string name, CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
        };

        await _repository.AddAsync(product);

        await _auditLogService.LogCreateAsync(
            nameof(Product),
            product.Id.ToString(),
            $"Created product '{product.Name}'.",
            cancellationToken);

        _logger.LogInformation("Product {ProductId} created.", product.Id);

        return ProductDto.FromEntity(product);
    }

    public async Task<ProductDto?> UpdateProductAsync(Guid id, string name, CancellationToken cancellationToken)
    {
        var updatedProduct = new Product { Id = id, Name = name };
        var product = await _repository.UpdateAsync(id, updatedProduct);

        if (product is null)
        {
            return null;
        }

        await _auditLogService.LogUpdateAsync(
            nameof(Product),
            product.Id.ToString(),
            $"Updated product '{product.Name}'.",
            cancellationToken);

        _logger.LogInformation("Product {ProductId} updated.", product.Id);

        return ProductDto.FromEntity(product);
    }

    public async Task<bool> DeleteProductAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await _repository.GetByIdAsync(id);
        if (product is null)
        {
            return false;
        }

        var success = await _repository.DeleteAsync(id);

        if (success)
        {
            await _auditLogService.LogDeleteAsync(
                nameof(Product),
                id.ToString(),
                $"Deleted product '{product.Name}'.",
                cancellationToken);

            _logger.LogInformation("Product {ProductId} deleted.", id);
        }

        return success;
    }
}
