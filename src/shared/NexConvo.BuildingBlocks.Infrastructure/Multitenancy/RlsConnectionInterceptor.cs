using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NexConvo.BuildingBlocks.Multitenancy;

/// <summary>
/// Sets <c>app.current_tenant_id</c> on every opened connection so PostgreSQL RLS policies
/// filter rows by tenant (skill Standard 6). Uses <c>set_config</c> with a bound parameter —
/// never string-concatenated SQL. Skips when no tenant is in scope (migrations, startup).
/// Each service registers this on its own DbContext; BuildingBlocks owns no DbContext.
/// </summary>
public sealed class RlsConnectionInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (!tenantContext.HasTenant)
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.current_tenant_id', @tenant, false)";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@tenant";
        parameter.Value = tenantContext.TenantId.ToString();
        command.Parameters.Add(parameter);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
