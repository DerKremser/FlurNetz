using System.Reflection;

namespace FlurNetz.Architecture.Tests;

/// <summary>
/// Liefert die eine bekannte Modul-Liste und ermittelt die zugehörigen Assemblies explizit.
/// </summary>
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

    /// <summary>
    /// Lädt ausschließlich einen bekannten Assembly-Namen; ein Dateisystem-Scan ist dadurch nicht nötig.
    /// </summary>
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
                "Engagement" or "Progression" =>
                    [ContractsAssemblyName, "FlurNetz.Modules.Identity.Contracts"],
                _ => [ContractsAssemblyName]
            };
    }
}
