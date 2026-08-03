using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Marsey.Core.Manifests;

namespace Marsey.Core.Discovery;

/// <summary>
/// Discovers one manifest per direct child directory without loading mod assemblies.
/// </summary>
public sealed class MarseyModDiscovery
{
    public const string ManifestFileName = "marsey.json";

    private readonly MarseyManifestReader _manifestReader;

    public MarseyModDiscovery(MarseyManifestReader? manifestReader = null)
    {
        _manifestReader = manifestReader ?? new MarseyManifestReader();
    }

    public MarseyDiscoveryResult Discover(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            return new MarseyDiscoveryResult(
                Array.Empty<MarseyModCandidate>(),
                new[]
                {
                    new MarseyDiscoveryIssue(null, "invalid-root", "The mod root directory is required.")
                });
        }

        string rootPath;
        try
        {
            rootPath = Path.GetFullPath(rootDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new MarseyDiscoveryResult(
                Array.Empty<MarseyModCandidate>(),
                new[]
                {
                    new MarseyDiscoveryIssue(rootDirectory, "invalid-root", exception.Message)
                });
        }

        if (!Directory.Exists(rootPath))
            return new MarseyDiscoveryResult(Array.Empty<MarseyModCandidate>(), Array.Empty<MarseyDiscoveryIssue>());

        var candidates = new List<MarseyModCandidate>();
        var issues = new List<MarseyDiscoveryIssue>();

        if (IsReparsePoint(
                rootPath,
                "mod-root-reparse-point",
                "The mod root directory cannot be a symbolic link or reparse point.",
                issues))
        {
            return new MarseyDiscoveryResult(candidates, issues);
        }

        var manifests = FindManifestPaths(rootPath, issues);
        var discoveredIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var manifestPath in manifests)
        {
            if (IsReparsePoint(
                    manifestPath,
                    "manifest-reparse-point",
                    "Manifest files cannot be symbolic links or reparse points.",
                    issues))
            {
                continue;
            }

            MarseyManifestReadResult readResult;
            try
            {
                using var stream = File.OpenRead(manifestPath);
                readResult = _manifestReader.Read(stream);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                issues.Add(new MarseyDiscoveryIssue(
                    manifestPath,
                    "manifest-read-failed",
                    exception.Message));
                continue;
            }

            if (!readResult.IsValid || readResult.Manifest is null)
            {
                issues.AddRange(readResult.Issues.Select(issue => new MarseyDiscoveryIssue(
                    manifestPath,
                    issue.Code,
                    issue.Message)));
                continue;
            }

            var manifest = readResult.Manifest;
            var modDirectory = Path.GetDirectoryName(manifestPath)!;

            string entryAssemblyPath;
            try
            {
                entryAssemblyPath = Path.GetFullPath(Path.Combine(modDirectory, manifest.EntryAssembly));
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                issues.Add(new MarseyDiscoveryIssue(
                    manifestPath,
                    "entry-assembly-path-invalid",
                    exception.Message));
                continue;
            }

            if (!IsInsideDirectory(modDirectory, entryAssemblyPath))
            {
                issues.Add(new MarseyDiscoveryIssue(
                    manifestPath,
                    "entry-assembly-outside-mod",
                    "The entry assembly resolved outside the mod directory."));
                continue;
            }

            if (!File.Exists(entryAssemblyPath))
            {
                issues.Add(new MarseyDiscoveryIssue(
                    manifestPath,
                    "entry-assembly-missing",
                    $"Entry assembly '{manifest.EntryAssembly}' does not exist."));
                continue;
            }

            if (IsReparsePoint(
                    entryAssemblyPath,
                    "entry-assembly-reparse-point",
                    "Entry assemblies cannot be symbolic links or reparse points.",
                    issues))
            {
                continue;
            }

            if (!discoveredIds.Add(manifest.Id))
            {
                issues.Add(new MarseyDiscoveryIssue(
                    manifestPath,
                    "duplicate-mod-id",
                    $"Another discovered mod already uses ID '{manifest.Id}'."));
                continue;
            }

            candidates.Add(new MarseyModCandidate(
                manifest,
                modDirectory,
                manifestPath,
                entryAssemblyPath));
        }

        candidates.Sort((left, right) => string.Compare(left.Manifest.Id, right.Manifest.Id, StringComparison.Ordinal));

        return new MarseyDiscoveryResult(candidates, issues);
    }

    private static IReadOnlyList<string> FindManifestPaths(
        string rootPath,
        ICollection<MarseyDiscoveryIssue> issues)
    {
        var manifestPaths = new List<string>();
        var rootManifest = Path.Combine(rootPath, ManifestFileName);
        if (File.Exists(rootManifest))
            manifestPaths.Add(rootManifest);

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(rootPath).OrderBy(path => path, StringComparer.Ordinal))
            {
                if (IsReparsePoint(
                        directory,
                        "mod-directory-reparse-point",
                        "Mod directories cannot be symbolic links or reparse points.",
                        issues))
                {
                    continue;
                }

                var manifestPath = Path.Combine(directory, ManifestFileName);
                if (File.Exists(manifestPath))
                    manifestPaths.Add(manifestPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            issues.Add(new MarseyDiscoveryIssue(rootPath, "mod-enumeration-failed", exception.Message));
        }

        manifestPaths.Sort(StringComparer.Ordinal);
        return manifestPaths;
    }

    private static bool IsReparsePoint(
        string path,
        string issueCode,
        string issueMessage,
        ICollection<MarseyDiscoveryIssue> issues)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0)
                return false;

            issues.Add(new MarseyDiscoveryIssue(path, issueCode, issueMessage));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            issues.Add(new MarseyDiscoveryIssue(path, "file-inspection-failed", exception.Message));
            return true;
        }
    }

    private static bool IsInsideDirectory(string directory, string path)
    {
        var relative = Path.GetRelativePath(directory, path);
        return !Path.IsPathRooted(relative) &&
               relative != ".." &&
               !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
               !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }
}
