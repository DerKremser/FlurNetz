using System.Reflection;
using FlurNetz.BuildingBlocks.Time;
using FlurNetz.Messaging.Integration;
using FlurNetz.Modules.Overlay;
using FlurNetz.Modules.Overlay.Application;
using FlurNetz.Modules.Overlay.Contracts;
using FlurNetz.Modules.Overlay.Migrations;
using FlurNetz.Modules.Overlay.Persistence;
using FlurNetz.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FlurNetz.Architecture.Tests;

/// <summary>Sichert die Ownership-, Secret- und Composition-Grenze des Overlay-Moduls.</summary>
public sealed class OverlayArchitectureTests
{
    [Fact]
    public void ImplementationReferencesOnlyOverlayFoundations()
    {
        var assembly = ModuleArchitectureCatalog.LoadAssembly("FlurNetz.Modules.Overlay");
        var references = assembly.GetReferencedAssemblies().Select(reference => reference.Name).Where(name => name is not null && name.StartsWith("FlurNetz.", StringComparison.Ordinal)).Select(name => name!).ToArray();
        Assert.All(references, reference => Assert.Contains(reference, new[] { "FlurNetz.Modules.Overlay.Contracts", "FlurNetz.BuildingBlocks", "FlurNetz.Persistence" }));
        Assert.DoesNotContain(references, reference => reference.Contains("Automation", StringComparison.Ordinal) || reference.Contains("Notifications", StringComparison.Ordinal) || reference.Contains("Shop", StringComparison.Ordinal) || reference.Contains("Economy", StringComparison.Ordinal) || reference.Contains("Identity", StringComparison.Ordinal));
    }

    [Fact]
    public void MigrationHasStableIdentityAndOnlyOwnedTables()
    {
        var migration = Assert.Single(new OverlayMigrationSource().GetMigrations());
        Assert.Equal("Overlay", migration.Owner);
        Assert.Equal(1L, migration.Version);
        Assert.Equal("CreateOverlayChannelsAndAlerts", migration.Name);
        Assert.Contains("overlay_channels", migration.Sql, StringComparison.Ordinal);
        Assert.Contains("overlay_alerts", migration.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("REFERENCES community_", migration.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RegistrationProvidesCapabilityAndMigrationWithoutConsumers()
    {
        var services = new ServiceCollection();
        services.AddOverlayModule();
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IOverlayAlertPublish));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IOverlayChannelStore) && descriptor.ImplementationType == typeof(PostgreSqlOverlayChannelStore));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IOverlayAlertStore) && descriptor.ImplementationType == typeof(PostgreSqlOverlayAlertStore));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IMigrationSource) && descriptor.ImplementationType == typeof(OverlayMigrationSource));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IIntegrationEventHandlerRegistration));
    }
}
