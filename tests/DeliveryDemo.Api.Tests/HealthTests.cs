using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DeliveryDemo.Api.Tests;

public sealed class HealthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    public HealthTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    [Fact]
    public async Task HealthEndpointReturnsApplicationReadiness()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("Healthy", health!.Status);
        Assert.Equal("DeliveryDemo.Api", health.Service);
    }

    [Fact]
    public async Task UnknownRoutesReturnNotFound()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/unknown")).StatusCode);
    }

    private sealed record HealthResponse(string Status, string Service);
}
