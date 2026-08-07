using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Chat.Application.Common;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Infrastructure.Services;

/// <summary>
/// Live reachability+auth probe for a channel connection's stored access token (Standard 22:
/// test-then-save). Uses <see cref="IChannelVerificationHttpClientFactory"/> to obtain a
/// Polly-wrapped (Standard 8) HttpClient per channel, then hits a lightweight, read-only endpoint
/// on the provider's API: Meta Graph `/me` for WhatsApp/Facebook/Instagram, Telegram's `getMe` for
/// Telegram. Web needs no external call — it always succeeds. TikTok/LinkedIn have no verification
/// endpoint wired yet, so they are reported as Failed with a clear "not supported" message rather
/// than a false Healthy. Never logs or returns the access token or the raw provider response body
/// (Standard 13) — only a sanitized, generic error is surfaced to callers.
/// </summary>
internal sealed class ChannelConnectionTester : IConnectionTester<ChannelTestInput>
{
    private readonly IChannelVerificationHttpClientFactory _clientFactory;
    private readonly ILogger<ChannelConnectionTester>? _logger;

    public ChannelConnectionTester(
        IChannelVerificationHttpClientFactory clientFactory,
        ILogger<ChannelConnectionTester>? logger = null)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public string IntegrationKind => "channel";

    public async Task<ConnectionHealth> TestAsync(ChannelTestInput input, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        // Web widget uses no external provider — nothing to probe.
        if (input.Channel == ChatChannel.Web)
            return ConnectionHealth.Healthy("Web widget", 0);

        if (input.Channel is ChatChannel.TikTok or ChatChannel.LinkedIn)
        {
            return ConnectionHealth.Failed(
                "Connection test not yet supported for this channel.", (int)sw.ElapsedMilliseconds);
        }

        try
        {
            var client = _clientFactory.CreateClient(input.Channel.ToLeadSourceChannel());

            return input.Channel switch
            {
                ChatChannel.WhatsApp or ChatChannel.Facebook or ChatChannel.Instagram =>
                    await ProbeMetaAsync(client, input.AccessToken, sw, ct),
                ChatChannel.Telegram => await ProbeTelegramAsync(client, input.AccessToken, sw, ct),
                _ => ConnectionHealth.Failed(
                    "Connection test not yet supported for this channel.", (int)sw.ElapsedMilliseconds),
            };
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(
                "Channel connection test failed for {Channel}: {ExceptionType}", input.Channel, ex.GetType().Name);
            return ConnectionHealth.Failed(
                $"Could not reach {ProviderName(input.Channel)}. Check the access token.",
                (int)sw.ElapsedMilliseconds);
        }
        catch (TaskCanceledException ex)
        {
            _logger?.LogWarning(
                "Channel connection test timed out for {Channel}: {ExceptionType}", input.Channel, ex.GetType().Name);
            return ConnectionHealth.Failed(
                $"Connection to {ProviderName(input.Channel)} timed out.", (int)sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(
                "Channel connection test failed for {Channel}: {ExceptionType}", input.Channel, ex.GetType().Name);
            return ConnectionHealth.Failed(
                $"Connection test failed for {ProviderName(input.Channel)}. Check the access token.",
                (int)sw.ElapsedMilliseconds);
        }
    }

    private static async Task<ConnectionHealth> ProbeMetaAsync(
        HttpClient client, string accessToken, Stopwatch sw, CancellationToken ct)
    {
        using var response = await client.GetAsync(
            $"me?fields=id,name&access_token={Uri.EscapeDataString(accessToken)}", ct);

        if (!response.IsSuccessStatusCode)
        {
            return ConnectionHealth.Failed(
                "Meta connection failed. Check the access token.", (int)sw.ElapsedMilliseconds);
        }

        var payload = await response.Content.ReadFromJsonAsync<MetaMeResponse>(ct);
        var detail = payload?.Name ?? payload?.Id ?? "Connected";
        return ConnectionHealth.Healthy(detail, (int)sw.ElapsedMilliseconds);
    }

    private static async Task<ConnectionHealth> ProbeTelegramAsync(
        HttpClient client, string accessToken, Stopwatch sw, CancellationToken ct)
    {
        using var response = await client.GetAsync($"bot{accessToken}/getMe", ct);

        if (!response.IsSuccessStatusCode)
        {
            return ConnectionHealth.Failed(
                "Telegram connection failed. Check the bot token.", (int)sw.ElapsedMilliseconds);
        }

        var payload = await response.Content.ReadFromJsonAsync<TelegramGetMeResponse>(ct);
        if (payload is not { Ok: true })
        {
            return ConnectionHealth.Failed(
                "Telegram connection failed. Check the bot token.", (int)sw.ElapsedMilliseconds);
        }

        var detail = payload.Result?.Username ?? payload.Result?.FirstName ?? "Connected";
        return ConnectionHealth.Healthy(detail, (int)sw.ElapsedMilliseconds);
    }

    private static string ProviderName(ChatChannel channel) => channel switch
    {
        ChatChannel.WhatsApp or ChatChannel.Facebook or ChatChannel.Instagram => "Meta",
        ChatChannel.Telegram => "Telegram",
        _ => "the provider",
    };

    private sealed record MetaMeResponse(string? Id, string? Name);

    private sealed record TelegramGetMeResponse(bool Ok, TelegramUser? Result);

    private sealed record TelegramUser(string? Username, string? FirstName);
}
