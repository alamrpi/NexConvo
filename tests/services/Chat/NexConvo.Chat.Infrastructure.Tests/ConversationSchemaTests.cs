using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.ValueObjects;
using NexConvo.Contracts.Enums;
using Npgsql;

namespace NexConvo.Chat.Infrastructure.Tests;

/// <summary>
/// Proves the AddConversationSchema migration produces the expected DDL — RLS enabled on every
/// new table and tenant isolation actually enforced, not just declared (Standard 6).
/// </summary>
[Collection("chat-postgres")]
public class ConversationSchemaTests(ChatPostgresFixture fixture)
{
    private async Task<T?> ScalarAsync<T>(string sql)
    {
        await using var conn = new NpgsqlConnection(fixture.SuperuserConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        return (T?)await cmd.ExecuteScalarAsync();
    }

    [Fact]
    public async Task RlsPolicy_BlocksCrossTenantConversationReads()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var seedContext = fixture.CreateTenantContext(tenantA))
        {
            var conversation = Conversation.StartAiHandling(tenantA, new ChannelIdentity(LeadSourceChannel.WhatsApp, "ext-1"), null);
            seedContext.Conversations.Add(conversation);
            await seedContext.SaveChangesAsync();
        }

        await using var tenantBContext = fixture.CreateTenantContext(tenantB);
        var visibleToTenantB = await tenantBContext.Conversations.ToListAsync();

        visibleToTenantB.Should().BeEmpty();
    }

    [Fact]
    public async Task RlsPolicy_AllowsSameTenantConversationReads()
    {
        var tenantA = Guid.NewGuid();

        Guid conversationId;
        await using (var seedContext = fixture.CreateTenantContext(tenantA))
        {
            var conversation = Conversation.StartAiHandling(tenantA, new ChannelIdentity(LeadSourceChannel.WhatsApp, "ext-2"), null);
            seedContext.Conversations.Add(conversation);
            await seedContext.SaveChangesAsync();
            conversationId = conversation.Id;
        }

        await using var readContext = fixture.CreateTenantContext(tenantA);
        var visible = await readContext.Conversations.ToListAsync();

        visible.Should().ContainSingle(c => c.Id == conversationId);
    }

    [Fact]
    public async Task RlsPolicy_BlocksCrossTenantMessageReads()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var seedContext = fixture.CreateTenantContext(tenantA))
        {
            var conversation = Conversation.StartAiHandling(tenantA, new ChannelIdentity(LeadSourceChannel.WhatsApp, "ext-3"), null);
            seedContext.Conversations.Add(conversation);
            var message = conversation.AppendInbound("provider-msg-1", "Hello");
            seedContext.Messages.Add(message);
            await seedContext.SaveChangesAsync();
        }

        await using var tenantBContext = fixture.CreateTenantContext(tenantB);
        var visibleToTenantB = await tenantBContext.Messages.ToListAsync();

        visibleToTenantB.Should().BeEmpty();
    }

    [Fact]
    public async Task RlsPolicy_BlocksCrossTenantEscalationReads()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var seedContext = fixture.CreateTenantContext(tenantA))
        {
            var conversation = Conversation.StartAiHandling(tenantA, new ChannelIdentity(LeadSourceChannel.WhatsApp, "ext-4"), null);
            seedContext.Conversations.Add(conversation);
            var escalation = conversation.RequestHandoff(EscalationReason.TriggerPhrase);
            seedContext.Escalations.Add(escalation);
            await seedContext.SaveChangesAsync();
        }

        await using var tenantBContext = fixture.CreateTenantContext(tenantB);
        var visibleToTenantB = await tenantBContext.Escalations.ToListAsync();

        visibleToTenantB.Should().BeEmpty();
    }

    [Theory]
    [InlineData("conversations")]
    [InlineData("messages")]
    [InlineData("escalations")]
    [Trait("Category", "Security")]
    public async Task RowLevelSecurity_IsEnabled(string table)
    {
        var enabled = await ScalarAsync<bool>(
            $"SELECT relrowsecurity FROM pg_class WHERE relname = '{table}'");

        enabled.Should().BeTrue($"{table} must have RLS enabled");
    }

    [Theory]
    [InlineData("conversations")]
    [InlineData("messages")]
    [InlineData("escalations")]
    [Trait("Category", "Security")]
    public async Task RowLevelSecurity_IsForced(string table)
    {
        var forced = await ScalarAsync<bool>(
            $"SELECT relforcerowsecurity FROM pg_class WHERE relname = '{table}'");

        forced.Should().BeTrue(
            $"{table} must FORCE row level security so RLS still applies if the runtime role ever becomes the table owner");
    }
}
