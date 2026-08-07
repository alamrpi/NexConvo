using System.Text.Json.Serialization;

namespace NexConvo.Chat.Domain.Enums;

public enum SentimentSensitivity
{
    [JsonStringEnumMemberName("low")]    Low    = 0,
    [JsonStringEnumMemberName("medium")] Medium = 1,
    [JsonStringEnumMemberName("high")]   High   = 2,
}
