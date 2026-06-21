using FluentAssertions;
using Logicc.AuditLogLib.Actors;
using Logicc.AuditLogLib.Contracts;
using Logicc.VictoriaLogSync.Clients;
using Logicc.VictoriaLogSync.Consumers;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Logicc.Test.VictoriaLogSync;

public class AuditLogConsumerTests
{
    private readonly Mock<IVictoriaLogsClient> _victoriaLogsClientMock = new();
    private readonly Mock<ILogger<AuditLogConsumer>> _loggerMock = new();

    private AuditLogConsumer CreateSut() => new(_victoriaLogsClientMock.Object, _loggerMock.Object);

    private static AuditLogMessage CreateMessage() => new()
    {
        Id = Guid.NewGuid(),
        Timestamp = DateTime.UtcNow,
        Action = "Delete",
        EntityName = "Product",
        EntityId = Guid.NewGuid().ToString(),
        Description = "Deleted product 'Widget'.",
        ActorType = ActorType.Admin,
        ActorIdentifier = "admin-1",
    };

    private static Mock<ConsumeContext<AuditLogMessage>> CreateConsumeContextMock(AuditLogMessage message)
    {
        var contextMock = new Mock<ConsumeContext<AuditLogMessage>>();
        contextMock.SetupGet(c => c.Message).Returns(message);
        contextMock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return contextMock;
    }

    [Fact]
    public async Task Consume_ForwardsMessageToVictoriaLogsClient()
    {
        var message = CreateMessage();
        var contextMock = CreateConsumeContextMock(message);

        var sut = CreateSut();
        await sut.Consume(contextMock.Object);

        _victoriaLogsClientMock.Verify(
            c => c.SendAsync(
                It.Is<AuditLogMessage>(m => m.Id == message.Id && m.Action == message.Action),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_PassesThroughTheExactMessageInstance()
    {
        AuditLogMessage? received = null;
        _victoriaLogsClientMock
            .Setup(c => c.SendAsync(It.IsAny<AuditLogMessage>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogMessage, CancellationToken>((msg, _) => received = msg)
            .Returns(Task.CompletedTask);

        var message = CreateMessage();
        var contextMock = CreateConsumeContextMock(message);

        var sut = CreateSut();
        await sut.Consume(contextMock.Object);

        received.Should().BeSameAs(message);
    }

    [Fact]
    public async Task Consume_WhenClientThrows_ExceptionPropagatesForMassTransitRetry()
    {
        _victoriaLogsClientMock
            .Setup(c => c.SendAsync(It.IsAny<AuditLogMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("VictoriaLogs unreachable"));

        var contextMock = CreateConsumeContextMock(CreateMessage());
        var sut = CreateSut();

        var act = async () => await sut.Consume(contextMock.Object);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
