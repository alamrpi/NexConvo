using System.Globalization;

namespace NexConvo.Chat.Application.Features.Conversations.Dtos;

/// <summary>
/// Opaque keyset-pagination cursor encoding a (timestamp, id) tie-break pair as
/// "{ticks}_{id}". Used by both GetConversationsQuery (ordered by UpdatedAt desc) and
/// GetConversationMessagesQuery (ordered by CreatedAt asc) — the caller decides sort direction,
/// this type only owns encode/decode of the pair.
/// </summary>
public static class ConversationCursor
{
    public static string Encode(DateTimeOffset timestamp, Guid id) =>
        $"{timestamp.UtcTicks.ToString(CultureInfo.InvariantCulture)}_{id:N}";

    public static bool TryDecode(string? cursor, out DateTimeOffset timestamp, out Guid id)
    {
        timestamp = default;
        id = default;

        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        var parts = cursor.Split('_', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
        {
            return false;
        }

        if (!Guid.TryParse(parts[1], out id))
        {
            return false;
        }

        timestamp = new DateTimeOffset(ticks, TimeSpan.Zero);
        return true;
    }
}
