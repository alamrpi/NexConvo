namespace NexConvo.BuildingBlocks.Domain;

/// <summary>Thrown when a domain invariant is violated. Caught at the API edge and mapped to a 4xx.</summary>
public sealed class DomainException(string message) : Exception(message);
