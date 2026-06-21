using FluentAssertions;
using Logicc.AuditLogLib.Actors;
using Logicc.AuditLogLib.Contracts;
using Logicc.AuditLogLib.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;


namespace Logicc.Test.AuditLogLib;

public class AdminLogServiceTests
{
    private readonly Mock<IActorContextProvider> _actorContextProviderMock = new();
    private readonly Mock<IBus> _busMock = new();
    private readonly Mock<IFileBufferLogger> _fileBufferLoggerMock = new();
    private readonly Mock<ILogger<AdminLogService>> _loggerMock = new();

    private AdminLogService CreateSut() =>
        new(_actorContextProviderMock.Object, _busMock.Object, _fileBufferLoggerMock.Object, _loggerMock.Object);

    private static AdminActorContext CreateAdminActor(string id = "admin-1") => new()
    {
        Id = id,
        Provider = AuthenticationProvider.Logicc,
    };

    private static UserActorContext CreateUserActor(Guid? userId = null) => new()
    {
        UserId = userId ?? Guid.NewGuid(),
        Provider = AuthenticationProvider.Logicc,
        TimeZoneMetadata = null,
    };

    [Fact]
    public async Task LogCreateAsync_WhenActorIsAdmin_PublishesAuditMessage()
    {
        var admin = CreateAdminActor("admin-42");
        _actorContextProviderMock.Setup(p => p.Context).Returns(admin);

        var sut = CreateSut();

        await sut.LogCreateAsync("Product", "product-1", "Created product 'Widget'.");

        _busMock.Verify(
            p => p.Publish(
                It.Is<AuditLogMessage>(m =>
                    m.Action == "Create" &&
                    m.EntityName == "Product" &&
                    m.EntityId == "product-1" &&
                    m.ActorType == ActorType.Admin &&
                    m.ActorIdentifier == "admin-42"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogUpdateAsync_WhenActorIsAdmin_PublishesAuditMessage()
    {
        var admin = CreateAdminActor("admin-7");
        _actorContextProviderMock.Setup(p => p.Context).Returns(admin);

        var sut = CreateSut();

        await sut.LogUpdateAsync("Product", "product-2", "Updated product 'Widget'.");

        _busMock.Verify(
            p => p.Publish(
                It.Is<AuditLogMessage>(m => m.Action == "Update" && m.ActorIdentifier == "admin-7"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogDeleteAsync_WhenActorIsAdmin_PublishesAuditMessage()
    {
        var admin = CreateAdminActor("admin-9");
        _actorContextProviderMock.Setup(p => p.Context).Returns(admin);

        var sut = CreateSut();

        await sut.LogDeleteAsync("Product", "product-3", "Deleted product 'Widget'.");

        _busMock.Verify(
            p => p.Publish(
                It.Is<AuditLogMessage>(m => m.Action == "Delete" && m.ActorIdentifier == "admin-9"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("Update")]
    [InlineData("Delete")]
    public async Task LogAsync_WhenActorIsPlainUser_DoesNotPublish(string action)
    {
        _actorContextProviderMock.Setup(p => p.Context).Returns(CreateUserActor());

        var sut = CreateSut();

        await InvokeAsync(sut, action);

        _busMock.Verify(
            p => p.Publish(It.IsAny<AuditLogMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LogCreateAsync_WhenActorIsTenantMember_DoesNotPublish()
    {
        var tenantMember = new TenantMemberActorContext
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ProductTier = ProductTier.Business,
            Provider = AuthenticationProvider.Logicc,
            TimeZoneMetadata = null,
        };
        _actorContextProviderMock.Setup(p => p.Context).Returns(tenantMember);

        var sut = CreateSut();

        await sut.LogCreateAsync("Product", "product-1", "Created product.");

        _busMock.Verify(
            p => p.Publish(It.IsAny<AuditLogMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LogCreateAsync_WhenActorIsApiKey_DoesNotPublish()
    {
        var apiKeyActor = new ApiKeyActorContext
        {
            TenantId = Guid.NewGuid(),
            Provider = AuthenticationProvider.Logicc,
        };
        _actorContextProviderMock.Setup(p => p.Context).Returns(apiKeyActor);

        var sut = CreateSut();

        await sut.LogCreateAsync("Product", "product-1", "Created product.");

        _busMock.Verify(
            p => p.Publish(It.IsAny<AuditLogMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LogCreateAsync_WhenActorIsService_DoesNotPublish()
    {
        var serviceActor = new ServiceActorContext
        {
            ServiceName = "background-worker",
            Provider = AuthenticationProvider.Logicc,
        };
        _actorContextProviderMock.Setup(p => p.Context).Returns(serviceActor);

        var sut = CreateSut();

        await sut.LogCreateAsync("Product", "product-1", "Created product.");

        _busMock.Verify(
            p => p.Publish(It.IsAny<AuditLogMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LogCreateAsync_WhenNoActorContext_DoesNotPublish()
    {
        _actorContextProviderMock.Setup(p => p.Context).Returns((ActorContext?)null);

        var sut = CreateSut();

        await sut.LogCreateAsync("Product", "product-1", "Created product.");

        _busMock.Verify(
            p => p.Publish(It.IsAny<AuditLogMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(typeof(AdminActorContext), ActorType.Admin)]
    public async Task LogCreateAsync_AdminActor_MapsActorTypeCorrectly(Type _, ActorType expectedActorType)
    {
        var admin = CreateAdminActor("admin-99");
        _actorContextProviderMock.Setup(p => p.Context).Returns(admin);

        AuditLogMessage? captured = null;
        _busMock
            .Setup(p => p.Publish(It.IsAny<AuditLogMessage>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogMessage, CancellationToken>((msg, _) => captured = msg)
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        await sut.LogCreateAsync("Product", "product-1", "Created product.");

        captured.Should().NotBeNull();
        captured!.ActorType.Should().Be(expectedActorType);
        captured.ActorIdentifier.Should().Be("admin-99");
        captured.Id.Should().NotBeEmpty();
        captured.Timestamp.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public async Task PublishAsync_WhenBusThrows_LogsErrorAndRethrows()
    {
        var message = new AuditLogMessage
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Action = "Create",
            EntityName = "Product",
            EntityId = "product-1",
            Description = "Created product.",
            ActorType = ActorType.Admin,
            ActorIdentifier = "admin-1",
        };

        _busMock
            .Setup(p => p.Publish(It.IsAny<AuditLogMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unavailable"));

        var sut = CreateSut();

        var act = async () => await sut.PublishAsync(message);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static Task InvokeAsync(IAdminLogService sut, string action) => action switch
    {
        "Create" => sut.LogCreateAsync("Product", "product-1", "desc"),
        "Update" => sut.LogUpdateAsync("Product", "product-1", "desc"),
        "Delete" => sut.LogDeleteAsync("Product", "product-1", "desc"),
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };
}
