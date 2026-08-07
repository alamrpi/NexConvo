using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;

namespace NexConvo.Chat.IntegrationTests;

/// <summary>
/// Authorization contract of ConversationsController: every endpoint requires authentication;
/// the two read endpoints require "conversations:read"; the four mutating endpoints require
/// "conversations:write"; and cross-tenant/non-assigned-agent access to a specific conversation
/// is refused via the same "not found" response used for a nonexistent id (spec: "without
/// revealing whether the conversation exists").
/// </summary>
[Collection("ChatApi")]
public class ConversationsControllerAuthorizationTests(ChatApiFactory factory)
{
    private HttpClient CreateClient(string? accessToken)
    {
        var client = factory.CreateClient();
        if (accessToken is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        return client;
    }

    [Fact]
    public async Task GetConversations_Unauthenticated_IsRejected()
    {
        using var client = CreateClient(accessToken: null);

        var response = await client.GetAsync("/api/v1/conversations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetConversations_MissingReadPermission_IsRejected()
    {
        var tenantId = Guid.NewGuid();
        var token = ChatApiFactory.CreateAccessToken(tenantId, Guid.NewGuid() /* no permissions */);
        using var client = CreateClient(token);

        var response = await client.GetAsync("/api/v1/conversations");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetConversations_WithReadPermission_Succeeds()
    {
        var tenantId = Guid.NewGuid();
        var token = ChatApiFactory.CreateAccessToken(tenantId, Guid.NewGuid(), "conversations:read");
        using var client = CreateClient(token);

        var response = await client.GetAsync("/api/v1/conversations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMessages_WrongTenant_ReturnsNotFound_WithoutRevealingExistence()
    {
        var ownerTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var conversationId = await factory.SeedConversationAsync(ownerTenant, $"sender-{Guid.NewGuid():N}");

        // Owner (*) of a different tenant must not reach ownerTenant's conversation.
        var token = ChatApiFactory.CreateAccessToken(otherTenant, Guid.NewGuid(), "*");
        using var client = CreateClient(token);

        var response = await client.GetAsync($"/api/v1/conversations/{conversationId}/messages");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(conversationId.ToString());
    }

    [Fact]
    public async Task GetMessages_MissingReadPermission_IsRejectedAtThePolicyLevel()
    {
        var tenantId = Guid.NewGuid();
        var conversationId = await factory.SeedConversationAsync(tenantId, $"sender-{Guid.NewGuid():N}");

        var token = ChatApiFactory.CreateAccessToken(tenantId, Guid.NewGuid() /* no permissions, not assigned */);
        using var client = CreateClient(token);

        var response = await client.GetAsync($"/api/v1/conversations/{conversationId}/messages");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SendReply_Unauthenticated_IsRejected()
    {
        var conversationId = Guid.NewGuid();
        using var client = CreateClient(accessToken: null);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/reply", new { text = "hi" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SendReply_MissingWritePermission_IsRejected()
    {
        var tenantId = Guid.NewGuid();
        var conversationId = await factory.SeedConversationAsync(tenantId, $"sender-{Guid.NewGuid():N}");

        // conversations:read alone must not satisfy the write policy on a mutating endpoint.
        var token = ChatApiFactory.CreateAccessToken(tenantId, Guid.NewGuid(), "conversations:read");
        using var client = CreateClient(token);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/reply", new { text = "hi" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SendReply_WrongTenant_ReturnsNotFound()
    {
        var ownerTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var conversationId = await factory.SeedConversationAsync(ownerTenant, $"sender-{Guid.NewGuid():N}");

        var token = ChatApiFactory.CreateAccessToken(otherTenant, Guid.NewGuid(), "conversations:write");
        using var client = CreateClient(token);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/reply", new { text = "hi" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TakeOver_MissingWritePermission_IsRejected()
    {
        var tenantId = Guid.NewGuid();
        var conversationId = await factory.SeedConversationAsync(tenantId, $"sender-{Guid.NewGuid():N}");

        var token = ChatApiFactory.CreateAccessToken(tenantId, Guid.NewGuid() /* no permissions */);
        using var client = CreateClient(token);

        var response = await client.PostAsync($"/api/v1/conversations/{conversationId}/take-over", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Resolve_MissingWritePermission_IsRejected()
    {
        var tenantId = Guid.NewGuid();
        var conversationId = await factory.SeedConversationAsync(tenantId, $"sender-{Guid.NewGuid():N}");

        var token = ChatApiFactory.CreateAccessToken(tenantId, Guid.NewGuid() /* no permissions */);
        using var client = CreateClient(token);

        var response = await client.PostAsync($"/api/v1/conversations/{conversationId}/resolve", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reopen_MissingWritePermission_IsRejected()
    {
        var tenantId = Guid.NewGuid();
        var conversationId = await factory.SeedConversationAsync(tenantId, $"sender-{Guid.NewGuid():N}");

        var token = ChatApiFactory.CreateAccessToken(tenantId, Guid.NewGuid() /* no permissions */);
        using var client = CreateClient(token);

        var response = await client.PostAsync($"/api/v1/conversations/{conversationId}/reopen", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reopen_WithWritePermission_OnAiHandlingConversation_ReturnsConflict()
    {
        // Reopen only accepts Resolved/Closed; a freshly seeded conversation is AiHandling, so the
        // domain's InvalidConversationStateTransitionException must surface as 409, not 500.
        var tenantId = Guid.NewGuid();
        var conversationId = await factory.SeedConversationAsync(tenantId, $"sender-{Guid.NewGuid():N}");

        var token = ChatApiFactory.CreateAccessToken(tenantId, Guid.NewGuid(), "conversations:write");
        using var client = CreateClient(token);

        var response = await client.PostAsync($"/api/v1/conversations/{conversationId}/reopen", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
