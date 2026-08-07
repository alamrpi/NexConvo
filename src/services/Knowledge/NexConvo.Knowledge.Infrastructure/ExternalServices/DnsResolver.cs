using System.Net;
using System.Net.Sockets;
using NexConvo.Knowledge.Application.Common.Interfaces;

namespace NexConvo.Knowledge.Infrastructure.ExternalServices;

/// <summary>Direct <see cref="Dns"/> wrapper backing the Url-source SSRF guard's host-resolution step.</summary>
internal sealed class DnsResolver : IDnsResolver
{
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            return addresses;
        }
        catch (SocketException)
        {
            return [];
        }
    }
}
