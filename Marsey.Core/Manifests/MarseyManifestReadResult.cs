using System.Collections.Generic;

namespace Marsey.Core.Manifests;

/// <summary>
/// A validation or parsing problem found in a Marsey manifest.
/// </summary>
public sealed record MarseyManifestIssue(string Code, string Message);

/// <summary>
/// Result of parsing and validating a Marsey manifest.
/// </summary>
public sealed record MarseyManifestReadResult(
    MarseyModManifest? Manifest,
    IReadOnlyList<MarseyManifestIssue> Issues)
{
    public bool IsValid => Manifest is not null && Issues.Count == 0;
}
