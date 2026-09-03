using FlurNetz.Modules.Administration.Contracts.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FlurNetz.Api.Administration;

public sealed record AdminPermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class AdminPermissionHandler(IAdminAuthenticationService authenticationService)
    : AuthorizationHandler<AdminPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminPermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || context.User.FindFirst(AdminAuthenticationDefaults.SchemeClaim)?.Value != AdminAuthenticationDefaults.Scheme
            || !PermissionCatalog.All.Contains(requirement.Permission)
            || !await authenticationService.ValidatePrincipalAsync(context.User).ConfigureAwait(false))
        {
            return;
        }

        context.Succeed(requirement);
    }
}

public static class AdminAuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddAdminAuthentication(this IServiceCollection services, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = AdminAuthenticationDefaults.Scheme;
                options.DefaultChallengeScheme = AdminAuthenticationDefaults.Scheme;
            })
            .AddCookie(AdminAuthenticationDefaults.Scheme, options =>
            {
                options.Cookie.Name = AdminAuthenticationDefaults.CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = environment.IsProduction()
                    ? CookieSecurePolicy.Always
                    : CookieSecurePolicy.SameAsRequest;
                options.Cookie.IsEssential = true;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                options.SlidingExpiration = true;
                options.LoginPath = "/admin/login";
                options.AccessDeniedPath = "/admin/forbidden";
                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = async context =>
                    {
                        var issuedUtc = context.Properties.IssuedUtc;
                        if (issuedUtc is null || issuedUtc.Value.AddHours(8) <= DateTimeOffset.UtcNow)
                        {
                            context.RejectPrincipal();
                            await context.HttpContext.SignOutAsync(AdminAuthenticationDefaults.Scheme).ConfigureAwait(false);
                            return;
                        }

                        var validator = context.HttpContext.RequestServices.GetRequiredService<IAdminAuthenticationService>();
                        if (!await validator.ValidatePrincipalAsync(context.Principal!, context.HttpContext.RequestAborted).ConfigureAwait(false))
                        {
                            context.RejectPrincipal();
                        }
                    },
                    OnRedirectToLogin = context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/api/admin", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        }

                        var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
                        context.Response.Redirect($"/admin/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
                        return Task.CompletedTask;
                    },
                    OnRedirectToAccessDenied = context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/api/admin", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            return Task.CompletedTask;
                        }

                        context.Response.Redirect("/admin/forbidden");
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = null;
            foreach (var permission in PermissionCatalog.All)
            {
                var policyName = AdminPolicies.ForPermission(permission);
                options.AddPolicy(policyName, policy =>
                {
                    policy.AuthenticationSchemes.Add(AdminAuthenticationDefaults.Scheme);
                    policy.RequireAuthenticatedUser();
                    if (!string.Equals(permission, PermissionCatalog.Access, StringComparison.Ordinal))
                    {
                        policy.AddRequirements(new AdminPermissionRequirement(PermissionCatalog.Access));
                    }
                    policy.AddRequirements(new AdminPermissionRequirement(permission));
                });
            }
        });
        services.AddScoped<IAuthorizationHandler, AdminPermissionHandler>();
        return services;
    }
}
