using System.Text.Json;
using FluentAssertions;
using NexConvo.Contracts.Enums;
using NexConvo.Contracts.Events.Crm;

namespace NexConvo.BuildingBlocks.Tests.Contracts;

public sealed class IntegrationEventSerializationTests
{
    [Fact]
    public void LeadCreated_RoundTripsThroughJson()
    {
        var original = new LeadCreatedIntegrationEvent
        {
            LeadId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ContactName = "Rahim Uddin",
            PhoneE164 = "+8801712345678",
            SourceChannel = LeadSourceChannel.WhatsApp,
        };

        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<LeadCreatedIntegrationEvent>(json);

        roundTripped.Should().NotBeNull();
        roundTripped!.LeadId.Should().Be(original.LeadId);
        roundTripped.TenantId.Should().Be(original.TenantId);
        roundTripped.PhoneE164.Should().Be(original.PhoneE164);
        roundTripped.SourceChannel.Should().Be(LeadSourceChannel.WhatsApp);
    }

    [Fact]
    public void NewEvent_HasGeneratedIdAndTimestamp()
    {
        var evt = new LeadCreatedIntegrationEvent
        {
            LeadId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ContactName = "Test",
            SourceChannel = LeadSourceChannel.Manual,
        };

        evt.EventId.Should().NotBe(Guid.Empty);
        evt.OccurredAt.Should().BeAfter(DateTimeOffset.MinValue);
    }
}
