namespace NexConvo.Chat.Application.Features.Widget.Dtos;

// No TenantId — the anonymous widget already holds its token; the internal tenant id is never
// leaked back to the public client (audit M2).
public sealed record WidgetConfigurationDto(
    string? WidgetIconUrl,
    string WidgetPrimaryColor,
    string WidgetSecondaryColor,
    string WidgetWelcomeMessage);
