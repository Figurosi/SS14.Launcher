using System.Collections.Generic;
using Marsey.Core.Manifests;

namespace Marsey.Core.Discovery;

public sealed record MarseyModCandidate(
    MarseyModManifest Manifest,
    string ModDirectory,
    string ManifestPath,
    string EntryAssemblyPath);

public sealed record MarseyDiscoveryIssue(
    string? SourcePath,
    string Code,
    string Message);

public sealed record MarseyDiscoveryResult(
    IReadOnlyList<MarseyModCandidate> Mods,
    IReadOnlyList<MarseyDiscoveryIssue> Issues);
