using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NexConvo.Notification.Infrastructure.Recipients;
using NSubstitute;

namespace NexConvo.Notification.Infrastructure.Tests;

public class HealthAlertRecipientsClientTests
{
    [Fact]
    public async Task Attaches_internal_api_key_header_and_hits_the_expected_path()
    {
        var tenantId = Guid.NewGuid();
        var handler = CapturingHttpMessageHandler.WithOk("""[{"name":"Owner One","email":"owner@tenant.test"},{"name":"Admin Two","email":"admin@tenant.test"}]""");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://identity:8080/") };
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("identity-internal").Returns(httpClient);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Internal:ApiKey"] = "localdev_internal_key",
            })
            .Build();

        var client = new HealthAlertRecipientsClient(httpClientFactory, configuration);

        var recipients = await client.GetRecipientsAsync(tenantId);

        var request = handler.CapturedRequest;
        request.Should().NotBeNull();
        request!.Method.Should().Be(HttpMethod.Get);
        request.RequestUri!.PathAndQuery.Should().Be($"/internal/tenants/{tenantId}/health-alert-recipients");
        request.Headers.GetValues("X-Internal-Api-Key").Should().ContainSingle().Which.Should().Be("localdev_internal_key");

        recipients.Should().HaveCount(2);
        recipients[0].Name.Should().Be("Owner One");
        recipients[0].Email.Should().Be("owner@tenant.test");
        recipients[1].Name.Should().Be("Admin Two");
        recipients[1].Email.Should().Be("admin@tenant.test");
    }
}
