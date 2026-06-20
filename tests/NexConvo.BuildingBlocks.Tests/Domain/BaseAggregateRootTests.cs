using FluentAssertions;
using NexConvo.BuildingBlocks.Domain;

namespace NexConvo.BuildingBlocks.Tests.Domain;

public sealed class BaseAggregateRootTests
{
    private sealed record SampleRaised(Guid Id) : IDomainEvent;

    private sealed class SampleAggregate : BaseAggregateRoot
    {
        public void DoSomething() => RaiseDomainEvent(new SampleRaised(Id));
    }

    [Fact]
    public void NewAggregate_HasIdentity()
    {
        new SampleAggregate().Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void RaiseDomainEvent_AccumulatesEvents()
    {
        var aggregate = new SampleAggregate();

        aggregate.DoSomething();
        aggregate.DoSomething();

        aggregate.DomainEvents.Should().HaveCount(2);
        aggregate.DomainEvents.Should().AllBeOfType<SampleRaised>();
    }

    [Fact]
    public void ClearDomainEvents_EmptiesTheCollection()
    {
        var aggregate = new SampleAggregate();
        aggregate.DoSomething();

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.Should().BeEmpty();
    }
}
