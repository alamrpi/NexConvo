using FluentAssertions;
using NexConvo.BuildingBlocks.Domain;

namespace NexConvo.BuildingBlocks.Tests.Domain;

public sealed class BaseEntityTests
{
    private sealed record SampleRaised(Guid Id) : IDomainEvent;

    private sealed class SampleRoot : BaseEntity
    {
        public void DoSomething() => RaiseDomainEvent(new SampleRaised(Id));
    }

    [Fact]
    public void NewEntity_HasIdentity()
    {
        // BaseEntity is deliberately NOT ITenantEntity — enforced at compile time
        // (a `SampleRoot is ITenantEntity` check would not compile), so it carries no RLS.
        new SampleRoot().Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void RaiseAndClear_DomainEvents_Works()
    {
        var entity = new SampleRoot();

        entity.DoSomething();
        entity.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<SampleRaised>();

        entity.ClearDomainEvents();
        entity.DomainEvents.Should().BeEmpty();
    }
}
