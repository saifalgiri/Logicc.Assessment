using Logicc.Api.Filters;
using Logicc.Api.IServices;
using Logicc.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Logicc.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>
    /// GET /api/products — readable by both Admin and User. Never produces an audit log,
    /// regardless of caller role, since read operations are explicitly out of scope for auditing.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts(CancellationToken cancellationToken)
    {
        var products = await _productService.GetAllProductsAsync(cancellationToken);
        return Ok(products);
    }

    /// <summary>
    /// GET /api/products/{id} — readable by both Admin and User. Never produces an audit log.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetProduct(Guid id, CancellationToken cancellationToken)
    {
        var product = await _productService.GetProductByIdAsync(id, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    /// <summary>
    /// POST /api/products — Admin only. Saves the new product, then publishes an audit log.
    /// </summary>
    [HttpPost]
    [AdminOnly]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProductDto>> CreateProduct(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _productService.CreateProductAsync(request.Name, cancellationToken);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    /// <summary>
    /// PUT /api/products/{id} — Admin only. Updates the product, then publishes an audit log.
    /// </summary>
    [HttpPut("{id:guid}")]
    [AdminOnly]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProductDto>> UpdateProduct(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _productService.UpdateProductAsync(id, request.Name, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    /// <summary>
    /// DELETE /api/products/{id} — Admin only. Deletes the product, then publishes an audit log.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [AdminOnly]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteProduct(Guid id, CancellationToken cancellationToken)
    {
        var success = await _productService.DeleteProductAsync(id, cancellationToken);
        return success ? NoContent() : NotFound();
    }
}
