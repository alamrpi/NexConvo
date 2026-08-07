using System.Net;
using System.Net.Sockets;

namespace NexConvo.Knowledge.Application.Common.Security;

/// <summary>
/// Pure IP-range check backing the Url-source SSRF guard. Blocks RFC-1918 private ranges,
/// loopback, and link-local (incl. 169.254.169.254 cloud metadata) for both IPv4 and IPv6.
/// The handler calls this AFTER resolving the URL's host via <see cref="Application.Common.Interfaces.IDnsResolver"/>
/// — string-matching the URL alone is insufficient because a hostname can resolve to a private
/// address at request time (DNS rebinding).
/// </summary>
public static class PrivateNetworkGuard
{
    /// <summary>True when <paramref name="address"/> is loopback, link-local, or a private range.</summary>
    public static bool IsPrivateOrLinkLocal(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily == AddressFamily.InterNetwork)
            return IsPrivateOrLinkLocalIPv4(address);

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || IsUniqueLocalIPv6(address);

        // Unknown address family — fail closed.
        return true;
    }

    private static bool IsPrivateOrLinkLocalIPv4(IPAddress address)
    {
        var b = address.GetAddressBytes();

        return b[0] == 10                                   // 10.0.0.0/8
            || (b[0] == 172 && b[1] is >= 16 and <= 31)      // 172.16.0.0/12
            || (b[0] == 192 && b[1] == 168)                  // 192.168.0.0/16
            || (b[0] == 169 && b[1] == 254)                  // 169.254.0.0/16 (incl. cloud metadata)
            || b[0] == 127;                                  // 127.0.0.0/8 loopback
    }

    private static bool IsUniqueLocalIPv6(IPAddress address)
    {
        // fc00::/7 — IPv6 unique local addresses (the IPv6 analogue of RFC-1918).
        var b = address.GetAddressBytes();
        return (b[0] & 0xFE) == 0xFC;
    }
}
