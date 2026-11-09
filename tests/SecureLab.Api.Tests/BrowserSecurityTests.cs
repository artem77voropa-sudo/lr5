using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace SecureLab.Api.Tests;

public sealed class BrowserSecurityTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    [Theory]
    [InlineData("/", "text/html")]
    [InlineData("/browser-lab.html", "text/html")]
    [InlineData("/auth-lab.html", "text/html")]
    public async Task ClientDocuments_HaveEnforcedCspAndHeaders(string route, string media)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(media, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("script-src 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.DoesNotContain("unsafe-inline", csp);
        Assert.DoesNotContain("*", csp);
        Assert.False(response.Headers.Contains("Content-Security-Policy-Report-Only"));
    }

    [Theory]
    [InlineData("http://localhost:4173", true)]
    [InlineData("http://localhost:4174", false)]
    [InlineData("http://localhost:4173.evil.example", false)]
    public async Task Preflight_UsesExactOriginAndLimitedCapabilities(string origin, bool allowed)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "x-lab-cors-probe");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        if (allowed)
        {
            Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
            Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
            Assert.Equal("GET", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Methods")));
            Assert.Equal("X-Lab-Cors-Probe", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Headers")), ignoreCase: true);
        }
    }

    [Fact]
    public async Task AnonymousProtectedGet_HasReadableCors401WithoutRedirect()
    {
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/incidents");
        request.Headers.Add("Origin", "http://localhost:4173");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("http://localhost:4173", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task Production_DoesNotEnableTeachingCorsOrigin()
    {
        using var host = factory.WithWebHostBuilder(b => b.UseEnvironment("Production"));
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "http://localhost:4173");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
