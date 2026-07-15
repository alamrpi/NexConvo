namespace NexConvo.Chat.Application.Features.Widget.Dtos;

public sealed record WidgetConfigurationDto(
    Guid TenantId,
    string? WidgetIconUrl,
    string WidgetPrimaryColor,
    string WidgetSecondaryColor,
    string WidgetWelcomeMessage);
