using System.Net;
using System.Net.Http.Json;
using System.Text;
using Himma.Tests.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Himma.Tests.Foundation;

/// <summary>Checks from the security checklist that are not covered by the feature tests.</summary>
public sealed class SecurityTests(HimmaApiFactory factory) : IClassFixture<HimmaApiFactory>
{
    [Fact]
    public async Task Unreadable_json_errors_do_not_reveal_internal_type_names()
    {
        var client = factory.CreateHttpsClient();

        var unknownField = await client.PostAsJsonAsync("/api/auth/login", new { username = "a", password = "b", isAdmin = true });
        var wrongType = await client.PostAsJsonAsync("/api/auth/login", new { username = 5, password = "b" });

        foreach (var response in new[] { unknownField, wrongType })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Himma.Api", body);
            Assert.DoesNotContain("System.", body);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
    }

    [Fact]
    public async Task Broken_json_is_rejected_with_problem_details()
    {
        var client = factory.CreateHttpsClient();

        var response = await client.PostAsync("/api/auth/login", new StringContent("{ \"username\": ", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotEmpty(problem!.Errors);
    }

    [Fact]
    public async Task Swagger_is_not_available_outside_development()
    {
        var client = factory.CreateHttpsClient(); // the test host runs as "Testing", not Development

        var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Admin_page_is_served_with_the_strict_content_security_policy()
    {
        var client = factory.CreateHttpsClient();

        var response = await client.GetAsync("/admin.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("script-src 'self'", csp);
        Assert.DoesNotContain("unsafe-eval", csp);
        Assert.DoesNotContain("<script>", await response.Content.ReadAsStringAsync());
    }
}
