using MediatR;
using NexConvo.Chat.Application.Features.Widget.Dtos;

namespace NexConvo.Chat.Application.Features.Widget.Queries;

public sealed record GetWidgetConfigurationQuery(Guid TenantId) : IRequest<WidgetConfigurationDto?>;
