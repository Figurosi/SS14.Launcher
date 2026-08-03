using System;
using System.Collections.Generic;

namespace Marsey.Core.Manifests;

/// <summary>
/// Defines when an extension may request its runtime integration to be activated.
/// </summary>
public enum MarseyLoadPhase
{
    BeforeContentAssemblies,
    AfterContentAssemblies,
    AfterClientStartup
}

/// <summary>
/// Validated, immutable metadata for a Marsey mod.
/// </summary>
public sealed record MarseyModManifest(
    string Id,
    string Name,
    Version Version,
    int ApiVersion,
    string EntryAssembly,
    string EntryType,
    Version? MinimumLauncherVersion,
    Version? MaximumLauncherVersionExclusive,
    MarseyLoadPhase LoadPhase,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> Conflicts);
