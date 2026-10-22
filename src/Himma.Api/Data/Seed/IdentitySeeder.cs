using Himma.Api.Domain;
using Himma.Api.Domain.Entities;
using Himma.Api.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Himma.Api.Data.Seed;

public static class IdentitySeeder
{
    /// <summary>Creates the roles and the configured admin account. Safe to run on every startup.</summary>
    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var admin = services.GetRequiredService<IOptions<AdminOptions>>().Value;
        if (string.IsNullOrWhiteSpace(admin.Username) || string.IsNullOrWhiteSpace(admin.Password))
        {
            logger.LogWarning("Admin:Username or Admin:Password is not configured; no admin account was seeded");
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = await userManager.FindByNameAsync(admin.Username);
        if (existing is not null)
        {
            // Never promote an existing account: someone could have registered the admin username first.
            if (!await userManager.IsInRoleAsync(existing, Roles.Admin))
            {
                logger.LogError("An account named {Username} exists but is not an admin. It was NOT promoted; choose a different Admin:Username", admin.Username);
            }
            return;
        }

        var now = DateTime.UtcNow;
        var user = new ApplicationUser
        {
            UserName = admin.Username,
            DisplayName = admin.DisplayName,
            CreatedAtUtc = now,
            PrivacyConsentAtUtc = now,
            IsActive = true,
            LockoutEnabled = true,
        };

        var created = await userManager.CreateAsync(user, admin.Password);
        if (!created.Succeeded)
        {
            // Error codes only; descriptions could echo input.
            logger.LogError("Admin account could not be created: {Errors}", string.Join(", ", created.Errors.Select(e => e.Code)));
            return;
        }

        await userManager.AddToRoleAsync(user, Roles.Admin);
        logger.LogInformation("Seeded admin account {Username}", admin.Username);
    }
}
