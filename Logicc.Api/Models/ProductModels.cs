using System.ComponentModel.DataAnnotations;
using Logicc.Api.Entities;

namespace Logicc.Api.Models;

public record ProductDto(Guid Id, string Name)
{
    public static ProductDto FromEntity(Product product) => new(product.Id, product.Name);
}

public class CreateProductRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
}

public class UpdateProductRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
}
