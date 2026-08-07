using System.Net;

namespace NexConvo.Knowledge.Application.Common.Interfaces;

/// <summary>
/// Thin abstraction over DNS resolution used by the Url-source SSRF guard (Standard 2 — constructor
/// injection over a static <see cref="System.Net.Dns"/> call, so the guard is unit-testable without a
/// real network hop). Implemented in Infrastructure as a direct <c>Dns.GetHostAddressesAsync</c> call.
/// </summary>
public interface IDnsResolver
{
    /// <summary>Resolves <paramref name="host"/> to its IP addresses. Empty when resolution fails.</summary>
    Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken);
}
