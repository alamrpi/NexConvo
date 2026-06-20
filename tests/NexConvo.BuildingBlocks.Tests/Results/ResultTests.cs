using FluentAssertions;
using NexConvo.BuildingBlocks.Results;

namespace NexConvo.BuildingBlocks.Tests.Results;

public sealed class ResultTests
{
    [Fact]
    public void Success_IsSuccessful()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Success);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void NotFound_IsFailureWithStatus()
    {
        var result = Result.NotFound("missing");

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Error.Should().Be("missing");
    }

    [Fact]
    public void Conflict_MapsToConflictStatus()
    {
        Result.Conflict().Status.Should().Be(ResultStatus.Conflict);
    }

    [Fact]
    public void SuccessOfT_CarriesValue()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void TypedFailure_HasNoValueAndCorrectStatus()
    {
        var result = Result<int>.NotFound();

        result.IsFailure.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Value.Should().Be(0);
    }
}
