using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Modules.Overlay.Application;
using FlurNetz.Modules.Overlay.Contracts;
using FlurNetz.Modules.Overlay.Migrations;
using FlurNetz.Modules.Overlay.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlurNetz.Modules.Overlay;

/// <summary>Registriert den persistierten Overlay-V1-Slice.</summary>
public static class OverlayModule
{
    /// <summary>Registriert Overlay-Stores, Use Cases, Capability und Migration.</summary>
    public static IServiceCollection AddOverlayModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IClock, SystemClock>();
        services.AddScoped<IOverlayChannelStore, PostgreSqlOverlayChannelStore>();
        services.AddScoped<IOverlayAlertStore, PostgreSqlOverlayAlertStore>();
        services.AddScoped<CreateOverlayChannel>();
        services.AddScoped<GetOverlayChannel>();
        services.AddScoped<ListOverlayChannels>();
        services.AddScoped<UpdateOverlayChannelMetadata>();
        services.AddScoped<EnableOverlayChannel>();
        services.AddScoped<DisableOverlayChannel>();
        services.AddScoped<ArchiveOverlayChannel>();
        services.AddScoped<RotateOverlaySourceKey>();
        services.AddScoped<OverlayAlertPublishCapability>();
        services.AddScoped<IOverlayAlertPublish>(provider => provider.GetRequiredService<OverlayAlertPublishCapability>());
        services.AddScoped<PublishOverlayAlert>();
        services.AddScoped<PublishPreviewAlert>();
        services.AddScoped<ResolveBrowserSource>();
        services.AddScoped<ReadStreamTail>();
        services.AddScoped<ReadAlertsAfterCursor>();
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IMigrationSource)
            && descriptor.ImplementationType == typeof(OverlayMigrationSource)))
        {
            services.AddSingleton<IMigrationSource, OverlayMigrationSource>();
        }

        return services;
    }
}
