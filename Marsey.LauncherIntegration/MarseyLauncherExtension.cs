using System;
using System.IO;
using System.Linq;
using Marsey.Core.Compatibility;
using Marsey.Core.Discovery;
using Serilog;
using SS14.Launcher.Extensibility;

namespace Marsey.LauncherIntegration;

/// <summary>
/// Builds a validated compatibility catalog for installed Marsey mods.
/// This initial integration does not load or execute mod assemblies.
/// </summary>
public sealed class MarseyLauncherExtension : ILauncherExtension
{
    public const int CurrentApiVersion = 1;

    private readonly string _modsDirectory;
    private readonly Version _launcherVersion;
    private readonly MarseyModDiscovery _discovery;
    private readonly MarseyCompatibilityEvaluator _compatibilityEvaluator;

    public MarseyLauncherExtension(
        string modsDirectory,
        Version launcherVersion,
        MarseyModDiscovery? discovery = null,
        MarseyCompatibilityEvaluator? compatibilityEvaluator = null)
    {
        if (string.IsNullOrWhiteSpace(modsDirectory))
            throw new ArgumentException("The Marsey mods directory is required.", nameof(modsDirectory));

        _modsDirectory = Path.GetFullPath(modsDirectory);
        _launcherVersion = launcherVersion ?? throw new ArgumentNullException(nameof(launcherVersion));
        _discovery = discovery ?? new MarseyModDiscovery();
        _compatibilityEvaluator = compatibilityEvaluator ?? new MarseyCompatibilityEvaluator();
    }

    public string Id => "marsey";

    public MarseyCatalog Catalog { get; private set; } = new(
        Array.Empty<MarseyCatalogEntry>(),
        Array.Empty<MarseyDiscoveryIssue>());

    public void Initialize()
    {
        Directory.CreateDirectory(_modsDirectory);

        var discoveryResult = _discovery.Discover(_modsDirectory);
        var entries = discoveryResult.Mods
            .Select(candidate => new MarseyCatalogEntry(
                candidate,
                _compatibilityEvaluator.Evaluate(
                    candidate.Manifest,
                    CurrentApiVersion,
                    _launcherVersion)))
            .ToArray();

        Catalog = new MarseyCatalog(entries, discoveryResult.Issues);

        var compatibleCount = entries.Count(entry => entry.Compatibility.IsCompatible);
        var incompatibleCount = entries.Length - compatibleCount;

        Log.Information(
            "Marsey catalog initialized from {ModsDirectory}: {CompatibleCount} compatible, " +
            "{IncompatibleCount} incompatible, {IssueCount} discovery issues",
            _modsDirectory,
            compatibleCount,
            incompatibleCount,
            discoveryResult.Issues.Count);
    }

    public void Shutdown()
    {
        // No runtime assemblies are loaded by this migration stage.
    }
}
