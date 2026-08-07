using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.Widget.Dtos;

namespace NexConvo.Chat.Application.Features.Widget.Queries;

/// <summary>
/// Reads a tenant's public widget configuration. The caller (an anonymous widget) has already had
/// its token resolved to a tenant id, but there's no ambient HTTP tenant context — so the read runs
/// through <see cref="IChatDbContextFactory"/> which pins the connection to the tenant and lets RLS
/// enforce isolation (Standard 6). This handler never bypasses RLS.
/// </summary>
public sealed class GetWidgetConfigurationQueryHandler(IChatDbContextFactory dbContextFactory)
    : IRequestHandler<GetWidgetConfigurationQuery, WidgetConfigurationDto?>
{
    public async Task<WidgetConfigurationDto?> Handle(GetWidgetConfigurationQuery request, CancellationToken ct)
    {
        await using var db = dbContextFactory.CreateForTenant(request.TenantId);

        var settings = await db.WorkspaceChatSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == request.TenantId, ct);

        if (settings is null) return null;

        return new WidgetConfigurationDto(
            settings.WidgetIconUrl,
            settings.WidgetPrimaryColor,
            settings.WidgetSecondaryColor,
            settings.WidgetWelcomeMessage);
    }
}
