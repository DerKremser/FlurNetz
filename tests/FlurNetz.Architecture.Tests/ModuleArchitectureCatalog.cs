using System.Reflection;

namespace FlurNetz.Architecture.Tests;

internal static class ModuleArchitectureCatalog
{
    public static IReadOnlyList<string> ExternalPlatformNames { get; } =
    [
        "Twitch",
        "Discord",
        "YouTube",
        "Kick",
        "StreamerBot"
    ];

    public static IReadOnlyList<ModuleDefinition> Modules { get; } =
    [
        new("Identity"),
        new("Engagement"),
        new("Progression"),
        new("Economy"),
        new("Rewards"),
        new("Inventory"),
        new("Titles"),
        new("Achievements"),
        new("Shop"),
        new("Notifications"),
        new("Automation"),
        new("Overlay"),
        new("Integrations"),
        new("Administration")
    ];

    public static Assembly LoadAssembly(string assemblyName) => Assembly.Load(new AssemblyName(assemblyName));

    internal sealed record ModuleDefinition(string Name)
    {
        public string ImplementationAssemblyName => $"FlurNetz.Modules.{Name}";
        public string ContractsAssemblyName => $"FlurNetz.Modules.{Name}.Contracts";
        public string ImplementationNamespace => $"FlurNetz.Modules.{Name}";
        public string ContractsNamespace => $"FlurNetz.Modules.{Name}.Contracts";

        public IReadOnlyList<string> AllowedImplementationModuleReferences =>
            Name switch
            {
                "Engagement" =>
                    [ContractsAssemblyName, "FlurNetz.Modules.Identity.Contracts"],
                "Progression" =>
                    [ContractsAssemblyName, "FlurNetz.Modules.Identity.Contracts", "FlurNetz.Modules.Engagement.Contracts"],
                "Economy" =>
                    [ContractsAssemblyName, "FlurNetz.Modules.Identity.Contracts"],
                "Rewards" =>
                    [
                        ContractsAssemblyName,
                        "FlurNetz.Modules.Identity.Contracts",
                        "FlurNetz.Modules.Economy.Contracts"
                    ],
                "Inventory" =>
                    [ContractsAssemblyName, "FlurNetz.Modules.Identity.Contracts"],
                "Shop" =>
                    [
                        ContractsAssemblyName,
                        "FlurNetz.Modules.Identity.Contracts",
                        "FlurNetz.Modules.Economy.Contracts",
                        "FlurNetz.Modules.Inventory.Contracts"
                    ],
                "Titles" =>
                    [ContractsAssemblyName, "FlurNetz.Modules.Identity.Contracts"],
                "Achievements" =>
                    [ContractsAssemblyName, "FlurNetz.Modules.Identity.Contracts"],
                _ => [ContractsAssemblyName]
            };
    }
}
