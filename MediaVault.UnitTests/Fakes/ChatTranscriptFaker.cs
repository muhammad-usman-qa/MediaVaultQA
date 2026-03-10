using Bogus;
using MediaVault.API.Models;

namespace MediaVault.UnitTests.Fakes;

/// <summary>
/// Generates realistic fake ChatTranscript and ChatMessage entities for testing.
/// Simulates customer support conversations with varied resolution outcomes.
/// </summary>
public class ChatTranscriptFaker : Faker<ChatTranscript>
{
    public ChatTranscriptFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.SessionId, f => f.Random.Guid().ToString());
        RuleFor(x => x.AgentId, f => $"agent-{f.Random.Int(100, 999)}");
        RuleFor(x => x.CustomerId, f => $"cust-{f.Random.Guid().ToString()[..8]}");
        RuleFor(x => x.CustomerName, f => f.Name.FullName());
        RuleFor(x => x.StartedAt, f => f.Date.Past(1).ToUniversalTime());
        RuleFor(x => x.EndedAt, (f, t) =>
            f.Random.Bool(0.7f) ? t.StartedAt.AddMinutes(f.Random.Double(2, 45)) : null);
        RuleFor(x => x.ResolutionStatus, f => f.PickRandom<ChatResolutionStatus>());
        RuleFor(x => x.SentimentScore, f => f.Random.Bool(0.8f) ? Math.Round(f.Random.Double(-1.0, 1.0), 2) : null);
        RuleFor(x => x.Messages, f => new ChatMessageFaker().Generate(f.Random.Int(2, 10)));
    }

    /// <summary>Creates a resolved transcript with positive sentiment.</summary>
    public ChatTranscript Resolved() =>
        RuleFor(x => x.ResolutionStatus, _ => ChatResolutionStatus.Resolved)
        .RuleFor(x => x.SentimentScore, f => Math.Round(f.Random.Double(0.3, 1.0), 2))
        .RuleFor(x => x.EndedAt, (f, t) => t.StartedAt.AddMinutes(f.Random.Double(5, 30)))
        .Generate();

    /// <summary>Creates an open transcript (conversation in progress).</summary>
    public ChatTranscript Open() =>
        RuleFor(x => x.ResolutionStatus, _ => ChatResolutionStatus.Open)
        .RuleFor(x => x.EndedAt, _ => (DateTime?)null)
        .Generate();
}

public class ChatMessageFaker : Faker<ChatMessage>
{
    private static readonly string[] AgentPhrases =
    [
        "How can I help you today?",
        "I understand your concern. Let me look into that.",
        "Thank you for your patience.",
        "I've resolved the issue on our end.",
        "Is there anything else I can assist you with?"
    ];

    private static readonly string[] CustomerPhrases =
    [
        "I'm having trouble accessing my files.",
        "The upload keeps failing.",
        "Can you explain how the search feature works?",
        "My audio file is not processing correctly.",
        "Thank you, that worked!"
    ];

    public ChatMessageFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.TranscriptId, f => f.Random.Guid());
        RuleFor(x => x.SenderType, f => f.PickRandom<MessageSenderType>());
        RuleFor(x => x.Sender, (f, m) => m.SenderType == MessageSenderType.Agent
            ? $"Agent_{f.Random.Int(100, 999)}"
            : f.Name.FirstName());
        RuleFor(x => x.Content, (f, m) => m.SenderType == MessageSenderType.Agent
            ? f.PickRandom(AgentPhrases)
            : f.PickRandom(CustomerPhrases));
        RuleFor(x => x.SentAt, f => f.Date.Past(1).ToUniversalTime());
        RuleFor(x => x.IsEdited, f => f.Random.Bool(0.05f));
    }
}
