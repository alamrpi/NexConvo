namespace NexConvo.BuildingBlocks.Rag;

/// <summary>
/// Recognizes greetings and small talk ("hi", "thanks", "bye") so callers can bypass the
/// grounding gate for them — these have no retrievable KB score and would otherwise always
/// trip the gate and return the tenant's NoAnswerMessage, which reads as broken to a user
/// who just said hello.
/// </summary>
public interface IGreetingDetector
{
    bool IsGreeting(string message);
}
