using MediatR;
using Microsoft.EntityFrameworkCore;
using NexConvo.BuildingBlocks.Application.Health;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.BuildingBlocks.Multitenancy;

namespace NexConvo.Integrations.Application.Features.S3Config.Commands;

public sealed class TestS3ConnectionCommandHandler(
    IIntegrationsDbContext db,
    ITenantContext tenant,
    IAesEncryptionService aes,
    IConnectionTester<S3TestInput> tester)
    : IRequestHandler<TestS3ConnectionCommand, ConnectionHealth>
{
    public async Task<ConnectionHealth> Handle(TestS3ConnectionCommand r, CancellationToken ct)
    {
        string ak = r.AccessKeyId ?? "", sk = r.SecretAccessKey ?? "";
        if (string.IsNullOrWhiteSpace(ak) || string.IsNullOrWhiteSpace(sk))
        {
            var stored = await db.WorkspaceS3Configs
                .FirstOrDefaultAsync(x => x.TenantId == tenant.TenantId, ct);
            if (stored is null)
                return ConnectionHealth.Failed("No S3 credentials configured.", null);
            ak = aes.Decrypt(stored.EncryptedAccessKeyId);
            sk = aes.Decrypt(stored.EncryptedSecretAccessKey);
        }
        return await tester.TestAsync(
            new S3TestInput(r.BucketName, r.Region, ak, sk, r.CustomEndpoint), ct);
    }
}
