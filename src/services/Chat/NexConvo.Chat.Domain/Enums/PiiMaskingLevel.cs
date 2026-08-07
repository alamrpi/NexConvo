using System.Text.Json.Serialization;

namespace NexConvo.Chat.Domain.Enums;

public enum PiiMaskingLevel
{
    [JsonStringEnumMemberName("off")]      Off      = 0,
    [JsonStringEnumMemberName("standard")] Standard = 1,
    [JsonStringEnumMemberName("strict")]   Strict   = 2,
}
