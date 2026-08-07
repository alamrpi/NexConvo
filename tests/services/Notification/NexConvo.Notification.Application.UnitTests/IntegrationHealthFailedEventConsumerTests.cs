using MassTransit;
using Microsoft.Extensions.Logging;
using NexConvo.Contracts.Events.Health;
using NexConvo.Notification.Application.Abstractions.Mailing;
using NexConvo.Notification.Application.Abstractions.Recipients;
using NexConvo.Notification.Application.Consumers;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace NexConvo.Notification.Application.UnitTests;

public class IntegrationHealthFailedEventConsumerTests
{
    private static IntegrationHealthFailedEvent Event() => new(
        TenantId: Guid.NewGuid(),
        IntegrationKind: "s3",
        ConfigId: Guid.NewGuid(),
        ConfigName: "primary-bucket",
        ErrorMessage: "Access denied",
        PreviousStatus: "Healthy");

    private static ConsumeContext<IntegrationHealthFailedEvent> FakeContext(IntegrationHealthFailedEvent message)
    {
        var context = Substitute.For<ConsumeContext<IntegrationHealthFailedEvent>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task Sends_an_email_to_each_recipient_when_recipients_exist()
    {
        var recipientsClient = Substitute.For<IHealthAlertRecipientsClient>();
        var sender = Substitute.For<IEmailSender>();
        var logger = Substitute.For<ILogger<IntegrationHealthFailedEventConsumer>>();
        var message = Event();

        recipientsClient.GetRecipientsAsync(message.TenantId, Arg.Any<CancellationToken>())
            .Returns(new List<RecipientDto>
            {
                new("Owner One", "owner@tenant.test"),
                new("Admin Two", "admin@tenant.test"),
            });

        var consumer = new IntegrationHealthFailedEventConsumer(recipientsClient, sender, logger);

        await consumer.Consume(FakeContext(message));

        await sender.Received(2).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        await sender.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.ToEmail == "owner@tenant.test" && m.Subject.Contains(message.IntegrationKind)),
            Arg.Any<CancellationToken>());
        await sender.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.ToEmail == "admin@tenant.test" && m.Subject.Contains(message.IntegrationKind)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Does_not_send_any_email_when_there_are_no_recipients()
    {
        var recipientsClient = Substitute.For<IHealthAlertRecipientsClient>();
        var sender = Substitute.For<IEmailSender>();
        var logger = Substitute.For<ILogger<IntegrationHealthFailedEventConsumer>>();
        var message = Event();

        recipientsClient.GetRecipientsAsync(message.TenantId, Arg.Any<CancellationToken>())
            .Returns(new List<RecipientDto>());

        var consumer = new IntegrationHealthFailedEventConsumer(recipientsClient, sender, logger);

        await consumer.Consume(FakeContext(message));

        await sender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Continues_sending_to_remaining_recipients_when_one_send_throws()
    {
        var recipientsClient = Substitute.For<IHealthAlertRecipientsClient>();
        var sender = Substitute.For<IEmailSender>();
        var logger = Substitute.For<ILogger<IntegrationHealthFailedEventConsumer>>();
        var message = Event();

        recipientsClient.GetRecipientsAsync(message.TenantId, Arg.Any<CancellationToken>())
            .Returns(new List<RecipientDto>
            {
                new("Owner One", "owner@tenant.test"),
                new("Admin Two", "admin@tenant.test"),
            });

        sender.SendAsync(
                Arg.Is<EmailMessage>(m => m.ToEmail == "owner@tenant.test"), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("SMTP unreachable"));

        var consumer = new IntegrationHealthFailedEventConsumer(recipientsClient, sender, logger);

        await consumer.Consume(FakeContext(message));

        await sender.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.ToEmail == "owner@tenant.test"), Arg.Any<CancellationToken>());
        await sender.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.ToEmail == "admin@tenant.test"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Propagates_exception_when_recipients_lookup_fails()
    {
        var recipientsClient = Substitute.For<IHealthAlertRecipientsClient>();
        var sender = Substitute.For<IEmailSender>();
        var logger = Substitute.For<ILogger<IntegrationHealthFailedEventConsumer>>();
        var message = Event();

        recipientsClient.GetRecipientsAsync(message.TenantId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Identity unreachable"));

        var consumer = new IntegrationHealthFailedEventConsumer(recipientsClient, sender, logger);

        var act = async () => await consumer.Consume(FakeContext(message));

        await Assert.ThrowsAsync<HttpRequestException>(act);
        await sender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }
}
