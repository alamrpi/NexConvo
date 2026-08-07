using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.IntegrationTests.Realtime;

/// <summary>
/// Deny-by-default contract of ChatHub: unauthenticated connections are refused at the handshake;
/// group membership requires tenant match (from the JWT, never the client) plus assigned-agent or
/// conversations:read permission; the agents dashboard group requires the permission policy.
/// </summary>
[Collection("ChatApi")]
public class ChatHubAuthorizationTests(ChatApiFactory factory)
{
    [Fact]
    public async Task UnauthenticatedConnection_IsRefused()
    {
        await using var connection = factory.CreateHubConnection(accessToken: null);

        var act = async () => await connection.StartAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CrossTenantJoin_IsRefused_WithoutRevealingExistence()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var conversationId = await factory.SeedConversationAsync(tenantA, $"sender-{Guid.NewGuid():N}");

        // Even an Owner (*) of tenant B must not reach tenant A's conversation.
        var tenantBToken = ChatApiFactory.CreateAccessToken(tenantB, Guid.NewGuid(), "*");
        await using var connection = factory.CreateHubConnection(tenantBToken);
        await connection.StartAsync();

        var act = async () => await connection.InvokeAsync("JoinConversation", conversationId);

        var refusal = await act.Should().ThrowAsync<HubException>();
        refusal.Which.Message.Should().NotContain(conversationId.ToString());
    }

    [Fact]
    public async Task SameTenant_NoPermission_NotAssigned_IsRefused()
    {
        var tenantId = Guid.NewGuid();
        var conversationId = await factory.SeedConversationAsync(tenantId, $"sender-{Guid.NewGuid():N}");

        var token = ChatApiFactory.CreateAccessToken(tenantId, Guid.NewGuid() /* no permissions */);
        await using var connection = factory.CreateHubConnection(token);
        await connection.StartAsync();

        var act = async () => await connection.InvokeAsync("JoinConversation", conversationId);

        await act.Should().ThrowAsync<HubException>();
    }

    [Fact]
    public async Task AssignedAgent_WithoutPermissionClaim_CanJoin()
    {
        var tenantId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var conversationId = await factory.SeedConversationAsync(tenantId, $"sender-{Guid.NewGuid():N}");

        await using (var db = factory.CreateSeedContext(tenantId))
        {
            var conversation = await db.Conversations.FirstAsync(c => c.Id == conversationId);
            db.Escalations.Add(conversation.RequestHandoff(EscalationReason.ExplicitAgentRequest));
            conversation.TakeOver(agentId);
            await db.SaveChangesAsync();
        }

        var token = ChatApiFactory.CreateAccessToken(tenantId, agentId /* no permissions — assignment suffices */);
        await using var connection = factory.CreateHubConnection(token);
        await connection.StartAsync();

        var act = async () => await connection.InvokeAsync("JoinConversation", conversationId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SameTenant_WithConversationsRead_CanJoin()
    {
        var tenantId = Guid.NewGuid();
        var conversationId = await factory.SeedConversationAsync(tenantId, $"sender-{Guid.NewGuid():N}");

        var token = ChatApiFactory.CreateAccessToken(tenantId, Guid.NewGuid(), "conversations:read");
        await using var connection = factory.CreateHubConnection(token);
        await connection.StartAsync();

        var act = async () => await connection.InvokeAsync("JoinConversation", conversationId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task JoinAgentDashboard_WithoutPermission_IsRefused()
    {
        var tenantId = Guid.NewGuid();

        var token = ChatApiFactory.CreateAccessToken(tenantId, Guid.NewGuid() /* no permissions */);
        await using var connection = factory.CreateHubConnection(token);
        await connection.StartAsync();

        var act = async () => await connection.InvokeAsync("JoinAgentDashboard");

        await act.Should().ThrowAsync<HubException>();
    }

    [Fact]
    public async Task JoinAgentDashboard_WithConversationsRead_Succeeds()
    {
        var tenantId = Guid.NewGuid();

        var token = ChatApiFactory.CreateAccessToken(tenantId, Guid.NewGuid(), "conversations:read");
        await using var connection = factory.CreateHubConnection(token);
        await connection.StartAsync();

        var act = async () => await connection.InvokeAsync("JoinAgentDashboard");

        await act.Should().NotThrowAsync();
    }
}
