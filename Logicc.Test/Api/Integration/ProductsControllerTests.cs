using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Logicc.Api.Models;
using Logicc.Test.Api.TestHelpers;
using Xunit;

namespace Logicc.Test.Api.Integration;

public class ProductsControllerTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ProductsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.RecordingAuditLogService.Clear();
        _factory.ProductRepository?.Clear();
    }

    public void Dispose()
    {
        _factory.RecordingAuditLogService.Clear();
        _factory.ProductRepository?.Clear();
    }

    private static HttpRequestMessage WithRole(HttpMethod method, string url, string? role)
    {
        var request = new HttpRequestMessage(method, url);
        if (role is not null)
        {
            request.Headers.Add("x-role", role);
        }
        return request;
    }

    // ---------- Authorization ----------

    [Fact]
    public async Task GetProducts_AsUser_Succeeds()
    {
        var response = await _client.SendAsync(WithRole(HttpMethod.Get, "/api/products", "user"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProducts_AsAdmin_Succeeds()
    {
        var response = await _client.SendAsync(WithRole(HttpMethod.Get, "/api/products", "admin"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateProduct_AsAdmin_Succeeds()
    {
        var request = WithRole(HttpMethod.Post, "/api/products", "admin");
        request.Content = JsonContent.Create(new CreateProductRequest { Name = "Admin-created widget" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateProduct_AsUser_IsForbidden()
    {
        var request = WithRole(HttpMethod.Post, "/api/products", "user");
        request.Content = JsonContent.Create(new CreateProductRequest { Name = "Should not be created" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateProduct_AsAdmin_Succeeds()
    {
        var productId = await CreateProductAsAdminAsync("Product to update");

        var request = WithRole(HttpMethod.Put, $"/api/products/{productId}", "admin");
        request.Content = JsonContent.Create(new UpdateProductRequest { Name = "Updated name" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateProduct_AsUser_IsForbidden()
    {
        var productId = await CreateProductAsAdminAsync("Product to (not) update");

        var request = WithRole(HttpMethod.Put, $"/api/products/{productId}", "user");
        request.Content = JsonContent.Create(new UpdateProductRequest { Name = "Should not apply" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteProduct_AsAdmin_Succeeds()
    {
        var productId = await CreateProductAsAdminAsync("Product to delete");

        var response = await _client.SendAsync(WithRole(HttpMethod.Delete, $"/api/products/{productId}", "admin"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteProduct_AsUser_IsForbidden()
    {
        var productId = await CreateProductAsAdminAsync("Product to (not) delete");
        _factory.RecordingAuditLogService.Clear();

        var response = await _client.SendAsync(WithRole(HttpMethod.Delete, $"/api/products/{productId}", "user"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------- Audit logging ----------

    [Fact]
    public async Task CreateProduct_AsAdmin_GeneratesAuditLog()
    {
        _factory.RecordingAuditLogService.Clear();

        var request = WithRole(HttpMethod.Post, "/api/products", "admin");
        request.Content = JsonContent.Create(new CreateProductRequest { Name = "Audited widget" });
        await _client.SendAsync(request);

        _factory.RecordingAuditLogService.Entries.Should().ContainSingle(e => e.Operation == "Create");
    }

    [Fact]
    public async Task UpdateProduct_AsAdmin_GeneratesAuditLog()
    {
        var productId = await CreateProductAsAdminAsync("Product to update");
        _factory.RecordingAuditLogService.Clear();

        var request = WithRole(HttpMethod.Put, $"/api/products/{productId}", "admin");
        request.Content = JsonContent.Create(new UpdateProductRequest { Name = "Updated audited name" });
        await _client.SendAsync(request);

        _factory.RecordingAuditLogService.Entries.Should().ContainSingle(e => e.Operation == "Update");
    }

    [Fact]
    public async Task DeleteProduct_AsAdmin_GeneratesAuditLog()
    {
        var productId = await CreateProductAsAdminAsync("Product to delete");
        _factory.RecordingAuditLogService.Clear();

        await _client.SendAsync(WithRole(HttpMethod.Delete, $"/api/products/{productId}", "admin"));

        _factory.RecordingAuditLogService.Entries.Should().ContainSingle(e => e.Operation == "Delete");
    }

    [Fact]
    public async Task GetProducts_AsAdmin_DoesNotGenerateAuditLog()
    {
        _factory.RecordingAuditLogService.Clear();

        await _client.SendAsync(WithRole(HttpMethod.Get, "/api/products", "admin"));

        _factory.RecordingAuditLogService.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProducts_AsUser_DoesNotGenerateAuditLog()
    {
        _factory.RecordingAuditLogService.Clear();

        await _client.SendAsync(WithRole(HttpMethod.Get, "/api/products", "user"));

        _factory.RecordingAuditLogService.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ForbiddenWriteAttempts_AsUser_NeverGenerateAuditLogs()
    {
        _factory.RecordingAuditLogService.Clear();

        var createRequest = WithRole(HttpMethod.Post, "/api/products", "user");
        createRequest.Content = JsonContent.Create(new CreateProductRequest { Name = "Attempted by user" });
        await _client.SendAsync(createRequest);

        var productId = await CreateProductAsAdminAsync("Existing product");
        _factory.RecordingAuditLogService.Clear();

        var updateRequest = WithRole(HttpMethod.Put, $"/api/products/{productId}", "user");
        updateRequest.Content = JsonContent.Create(new UpdateProductRequest { Name = "Attempted update" });
        await _client.SendAsync(updateRequest);

        await _client.SendAsync(WithRole(HttpMethod.Delete, $"/api/products/{productId}", "user"));

        _factory.RecordingAuditLogService.Entries.Should().BeEmpty();
    }

    private async Task<Guid> CreateProductAsAdminAsync(string name)
    {
        var request = WithRole(HttpMethod.Post, "/api/products", "admin");
        request.Content = JsonContent.Create(new CreateProductRequest { Name = name });

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var product = await response.Content.ReadFromJsonAsync<ProductDto>();
        return product!.Id;
    }
}
