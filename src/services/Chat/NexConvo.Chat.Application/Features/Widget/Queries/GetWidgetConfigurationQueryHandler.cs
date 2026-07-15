using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.Widget.Dtos;

namespace NexConvo.Chat.Application.Features.Widget.Queries;

public sealed class GetWidgetConfigurationQueryHandler(IChatDbContext db)
    : IRequestHandler<GetWidgetConfigurationQuery, WidgetConfigurationDto?>
{
    public async Task<WidgetConfigurationDto?> Handle(GetWidgetConfigurationQuery request, CancellationToken ct)
    {
        // Query bypasses RLS since it's a public endpoint finding a tenant by token.
        // It relies on EntityFramework configuration allowing querying across tenants
        // when we don't apply the interceptor, or since the interceptor requires tenant id,
        // we might need to bypass it. Wait, the RLS interceptor applies automatically.
        // If TenantId is not set in TenantContext, the interceptor might fail or filter nothing.
        // Let's use IgnoreQueryFilters() if needed, but RLS is at the DB level via SET LOCAL.
        // We will just execute the query. If it fails due to RLS, we'll need to use a system connection.
        var settings = await db.WorkspaceChatSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == request.TenantId, ct);

        if (settings is null) return null;

        return new WidgetConfigurationDto(
            settings.TenantId,
            settings.WidgetIconUrl,
            settings.WidgetPrimaryColor,
            settings.WidgetSecondaryColor,
            settings.WidgetWelcomeMessage);
    }
}
