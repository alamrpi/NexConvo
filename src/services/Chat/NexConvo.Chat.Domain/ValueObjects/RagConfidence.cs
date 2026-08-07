using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Domain.ValueObjects;

public sealed record RagConfidence(double Score, ConfidenceBand Band)
{
    public static RagConfidence FromRetrievalAndAbstention(double topRetrievalScore, bool modelAbstained)
    {
        if (modelAbstained)
        {
            return new RagConfidence(topRetrievalScore, ConfidenceBand.Low);
        }

        var band = topRetrievalScore switch
        {
            >= 0.8 => ConfidenceBand.High,
            >= 0.5 => ConfidenceBand.Medium,
            _ => ConfidenceBand.Low,
        };

        return new RagConfidence(topRetrievalScore, band);
    }
}
