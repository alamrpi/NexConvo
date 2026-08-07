using System.Text.RegularExpressions;

namespace NexConvo.BuildingBlocks.Rag;

public sealed partial class GreetingDetector : IGreetingDetector
{
    // English + Bengali (first-class per project standards) greetings and small talk. Matches the
    // whole message (after trimming trailing punctuation) so "hi, what's your refund policy?" is
    // still routed through retrieval — only pure greetings/small-talk bypass the gate.
    private static readonly string[] Phrases =
    [
        "hi", "hii", "hiya", "hello", "hey", "hey there", "hi there",
        "good morning", "good afternoon", "good evening",
        "how are you", "how are you doing", "what's up", "whats up",
        "thanks", "thank you", "thx", "ok thanks", "okay thanks",
        "bye", "goodbye", "see you", "see ya",
        "হাই", "হ্যালো", "শুভ সকাল", "কেমন আছেন", "কেমন আছো",
        "ধন্যবাদ", "বিদায়",
    ];

    public bool IsGreeting(string message)
    {
        var normalized = NormalizeRegex().Replace(message.Trim().ToLowerInvariant(), "").Trim();
        return normalized.Length > 0 && Phrases.Contains(normalized);
    }

    [GeneratedRegex(@"[!.?,]+$")]
    private static partial Regex NormalizeRegex();
}
