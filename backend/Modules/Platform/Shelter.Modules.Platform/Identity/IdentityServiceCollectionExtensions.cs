using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Platform.Domain;
using Shelter.Modules.Platform.Features.Memberships;

namespace Shelter.Modules.Platform.Identity;

/// <summary>ASP.NET Identity over the composed <see cref="ShelterDbContext"/> (ADR 0007, 0018). No Identity roles.</summary>
internal static class IdentityServiceCollectionExtensions
{
    /// <summary>How often the session cookie is re-checked against the account's security stamp. Default 1 minute.</summary>
    public const string SecurityStampIntervalKey = "Auth:SecurityStampValidationInterval";

    public static IServiceCollection AddShelterIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddIdentityCore<UserAccount>(options =>
            {
                // NIST SP 800-63B: length, not composition rules.
                options.Password.RequiredLength = 12;
                options.Password.RequiredUniqueChars = 1;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

                // The user name is the email, validated as an email; no character allowlist on top.
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = string.Empty;
            })
            .AddUserStore<UserOnlyStore<UserAccount, ShelterDbContext, Guid>>()
            .AddSignInManager<ShelterSignInManager>()
            .AddDefaultTokenProviders()
            .AddClaimsPrincipalFactory<ShelterClaimsPrincipalFactory>();

        services.Configure<SecurityStampValidatorOptions>(options =>
        {
            options.ValidationInterval = configuration.GetValue(SecurityStampIntervalKey, TimeSpan.FromMinutes(1));

            // The refreshed principal is rebuilt from the account; keep the session's own claims (active
            // organization, how it signed in).
            options.OnRefreshingPrincipal = context =>
            {
                if (context.CurrentPrincipal is { } current && context.NewPrincipal?.Identity is System.Security.Claims.ClaimsIdentity identity)
                {
                    foreach (var claim in current.FindAll(c => PlatformClaims.SessionClaims.Contains(c.Type)))
                    {
                        identity.AddClaim(new System.Security.Claims.Claim(claim.Type, claim.Value));
                    }
                }

                return Task.CompletedTask;
            };
        });

        services.AddScoped<SecurityEventWriter>();
        services.AddScoped<StaffMembershipDirectory>();

        // Replaces the building blocks' no-tenant fallback.
        services.Replace(ServiceDescriptor.Singleton<ITenantResolver, MembershipTenantResolver>());
        return services;
    }
}
