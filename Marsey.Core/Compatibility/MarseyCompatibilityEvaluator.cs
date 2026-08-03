using System;
using Marsey.Core.Manifests;

namespace Marsey.Core.Compatibility;

public enum MarseyCompatibilityStatus
{
    Compatible,
    UnsupportedApiVersion,
    LauncherTooOld,
    LauncherTooNew
}

public sealed record MarseyCompatibilityResult(
    MarseyCompatibilityStatus Status,
    string? Message)
{
    public bool IsCompatible => Status == MarseyCompatibilityStatus.Compatible;
}

/// <summary>
/// Evaluates manifest compatibility without loading or inspecting the mod assembly.
/// </summary>
public sealed class MarseyCompatibilityEvaluator
{
    public MarseyCompatibilityResult Evaluate(
        MarseyModManifest manifest,
        int supportedApiVersion,
        Version launcherVersion)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(launcherVersion);

        if (manifest.ApiVersion != supportedApiVersion)
        {
            return new MarseyCompatibilityResult(
                MarseyCompatibilityStatus.UnsupportedApiVersion,
                $"Mod API {manifest.ApiVersion} is not supported; this launcher supports API {supportedApiVersion}.");
        }

        if (manifest.MinimumLauncherVersion is not null &&
            launcherVersion.CompareTo(manifest.MinimumLauncherVersion) < 0)
        {
            return new MarseyCompatibilityResult(
                MarseyCompatibilityStatus.LauncherTooOld,
                $"Launcher {launcherVersion} is older than required version {manifest.MinimumLauncherVersion}.");
        }

        if (manifest.MaximumLauncherVersionExclusive is not null &&
            launcherVersion.CompareTo(manifest.MaximumLauncherVersionExclusive) >= 0)
        {
            return new MarseyCompatibilityResult(
                MarseyCompatibilityStatus.LauncherTooNew,
                $"Launcher {launcherVersion} is outside the supported range ending before {manifest.MaximumLauncherVersionExclusive}.");
        }

        return new MarseyCompatibilityResult(MarseyCompatibilityStatus.Compatible, null);
    }
}
