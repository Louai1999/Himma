using Himma.Api.Data;
using Himma.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Himma.Api.Infrastructure.Identity;

public static class IdentitySetup
{
    /// <summary>
    /// Identity core only (no cookie auth); sign-in is JWT based, see AuthSetup.
    /// Passwords are hashed by Identity's PBKDF2 hasher.
    /// </summary>
    public static IServiceCollection AddHimmaIdentity(this IServiceCollection services)
    {
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                // Length matters more than character classes; common passwords are blocked by CommonPasswordValidator.
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 4;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                // Email is optional (accounts created before email existed have none); confirmed emails are
                // kept unique in code (see AccountEmailController).
                options.User.RequireUniqueEmail = false;

                // Six-digit codes sent by email, tied to the security stamp: they stop working as soon as the
                // password or the email changes, so each code can only be used once.
                options.Tokens.ChangeEmailTokenProvider = TokenOptions.DefaultEmailProvider;
                options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultEmailProvider;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddErrorDescriber<LocalizedIdentityErrorDescriber>()
            .AddPasswordValidator<CommonPasswordValidator>()
            .AddDefaultTokenProviders();

        return services;
    }
}
