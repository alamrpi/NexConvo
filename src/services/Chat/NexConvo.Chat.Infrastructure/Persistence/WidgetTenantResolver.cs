using Microsoft.Extensions.Configuration;
using NexConvo.Chat.Application.Common.Interfaces;
using Npgsql;

namespace NexConvo.Chat.Infrastructure.Persistence;

/// <summary>
/// Resolves a public widget token to its tenant via the <c>resolve_widget_tenant</c> SECURITY
/// DEFINER function — the single RLS-exempt lookup the anonymous widget path is allowed (Standard 6).
/// The service role has EXECUTE on that function only, so this code can never table-scan
/// <c>workspace_chat_settings</c> across tenants; the function returns exactly one tenant id or NULL.
/// </summary>
public sealed class WidgetTenantResolver(IConfiguration configuration) : IWidgetTenantResolver
{
    private readonly string _connectionString = configuration.GetConnectionString("ChatDb")
        ?? throw new InvalidOperationException("Connection string 'ChatDb' is not configured.");

    public async Task<Guid?> ResolveAsync(Guid widgetToken, CancellationToken ct)
    {
        if (widgetToken == Guid.Empty)
        {
            return null;
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT resolve_widget_tenant(@token)";
        command.Parameters.AddWithValue("token", widgetToken);

        var result = await command.ExecuteScalarAsync(ct);
        return result is Guid tenantId ? tenantId : null;
    }
}
