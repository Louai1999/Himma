using Himma.Api.Data;
using Himma.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Himma.Api.Infrastructure.Identity;

public static class IdentitySetup
{
    /// <summary>
    /// Identity core only (no cookie auth). JWT bearer auth and the sign-in flow are added in phase 2.
    /// Passwords are hashed by Identity's PBKDF2 hasher.
    /// </summary>
    public static IServiceCollection AddHimmaIdentity(this IServiceCollection services)
    {
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                // Length matters more than character classes; very common passwords are blocked in phase 2.
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 4;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.User.RequireUniqueEmail = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        return services;
    }
}
