using System.Net;
using Himma.Api.Data;
using Himma.Api.Data.Seed;
using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Himma.Tests.Foundation;

public sealed class FoundationTests(HimmaApiFactory factory) : IClassFixture<HimmaApiFactory>
{
    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/")]
    [InlineData("/api/does-not-exist")]
    public async Task Every_response_has_security_headers(string path)
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("script-src 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.True(response.Headers.Contains("Permissions-Policy"));
    }

    [Fact]
    public async Task Static_index_page_is_served()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("dir=\"rtl\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_api_route_returns_problem_details()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Seeding_creates_roles_foods_and_admin()
    {
        factory.CreateClient(); // ensures the app (and the seeders) has started

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        Assert.True(await roles.RoleExistsAsync(Roles.User));
        Assert.True(await roles.RoleExistsAsync(Roles.Admin));
        Assert.True(await db.Foods.CountAsync() >= 55);

        var admin = await users.FindByNameAsync(HimmaApiFactory.AdminUsername);
        Assert.NotNull(admin);
        Assert.True(admin.IsActive);
        Assert.True(await users.IsInRoleAsync(admin, Roles.Admin));
        Assert.NotEqual(factory.AdminPassword, admin.PasswordHash);
    }

    [Fact]
    public async Task Seeding_twice_does_not_duplicate_anything()
    {
        factory.CreateClient();

        await factory.Services.InitializeDatabaseAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(FoodSeedData.All.Count, await db.Foods.CountAsync());
        Assert.Equal(1, await db.Users.CountAsync(u => u.UserName == HimmaApiFactory.AdminUsername));
        Assert.Equal(2, await db.Roles.CountAsync());
    }

    [Fact]
    public void Seed_food_names_are_unique()
    {
        var names = FoodSeedData.All.Select(f => f.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }
}
