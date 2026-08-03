using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Marsey.Core.Manifests;

/// <summary>
/// Parses untrusted manifest JSON into a validated <see cref="MarseyModManifest" />.
/// This class performs metadata validation only and never loads the declared assembly.
/// </summary>
public sealed class MarseyManifestReader
{
    public const int MaximumManifestBytes = 256 * 1024;
    private const int MaximumJsonDepth = 16;

    private static readonly Regex IdentifierPattern = new(
        "^[a-z0-9][a-z0-9._-]{2,127}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = MaximumJsonDepth,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) }
    };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
        MaxDepth = MaximumJsonDepth
    };

    public MarseyManifestReadResult Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
            return Invalid("manifest-not-readable", "The manifest stream is not readable.");

        using var boundedData = ReadBounded(stream);
        if (boundedData is null)
        {
            return Invalid(
                "manifest-too-large",
                $"The manifest exceeds the {MaximumManifestBytes}-byte size limit.");
        }

        var shapeIssue = ValidateJsonShape(boundedData);
        if (shapeIssue is not null)
            return new MarseyManifestReadResult(null, new[] { shapeIssue });

        ManifestDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<ManifestDocument>(boundedData, SerializerOptions);
        }
        catch (JsonException exception)
        {
            return Invalid("invalid-json", exception.Message);
        }

        if (document is null)
            return Invalid("empty-manifest", "The manifest did not contain a JSON object.");

        var issues = new List<MarseyManifestIssue>();

        ValidateIdentifier(document.Id, "id", issues);
        ValidateRequiredText(document.Name, "name", 128, issues);
        ValidateRequiredText(document.EntryType, "entryType", 512, issues);

        if (!Version.TryParse(document.Version, out var version))
            issues.Add(new MarseyManifestIssue("invalid-version", "version must be a valid dotted version."));

        if (document.ApiVersion is null)
        {
            issues.Add(new MarseyManifestIssue("missing-field", "apiVersion is required."));
        }
        else if (document.ApiVersion <= 0)
        {
            issues.Add(new MarseyManifestIssue("invalid-api-version", "apiVersion must be greater than zero."));
        }

        ValidateEntryAssembly(document.EntryAssembly, issues);

        var minimumLauncherVersion = ParseOptionalVersion(
            document.MinimumLauncherVersion,
            "minimumLauncherVersion",
            issues);
        var maximumLauncherVersionExclusive = ParseOptionalVersion(
            document.MaximumLauncherVersionExclusive,
            "maximumLauncherVersionExclusive",
            issues);

        if (minimumLauncherVersion is not null &&
            maximumLauncherVersionExclusive is not null &&
            minimumLauncherVersion.CompareTo(maximumLauncherVersionExclusive) >= 0)
        {
            issues.Add(new MarseyManifestIssue(
                "invalid-launcher-range",
                "minimumLauncherVersion must be lower than maximumLauncherVersionExclusive."));
        }

        var dependencies = NormalizeReferences(document.Dependencies, "dependencies", issues);
        var conflicts = NormalizeReferences(document.Conflicts, "conflicts", issues);

        if (!string.IsNullOrWhiteSpace(document.Id))
        {
            if (dependencies.Contains(document.Id, StringComparer.Ordinal))
                issues.Add(new MarseyManifestIssue("self-dependency", "A mod cannot depend on itself."));

            if (conflicts.Contains(document.Id, StringComparer.Ordinal))
                issues.Add(new MarseyManifestIssue("self-conflict", "A mod cannot conflict with itself."));
        }

        foreach (var dependency in dependencies.Intersect(conflicts, StringComparer.Ordinal))
        {
            issues.Add(new MarseyManifestIssue(
                "dependency-conflict-overlap",
                $"'{dependency}' cannot be both a dependency and a conflict."));
        }

        if (issues.Count > 0 || version is null || document.ApiVersion is null)
            return new MarseyManifestReadResult(null, issues);

        var manifest = new MarseyModManifest(
            document.Id!,
            document.Name!,
            version,
            document.ApiVersion.Value,
            document.EntryAssembly!,
            document.EntryType!,
            minimumLauncherVersion,
            maximumLauncherVersionExclusive,
            document.LoadPhase ?? MarseyLoadPhase.AfterContentAssemblies,
            dependencies,
            conflicts);

        return new MarseyManifestReadResult(manifest, Array.Empty<MarseyManifestIssue>());
    }

    private static MarseyManifestIssue? ValidateJsonShape(MemoryStream data)
    {
        try
        {
            using var document = JsonDocument.Parse(data, DocumentOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new MarseyManifestIssue(
                    "invalid-json-root",
                    "The manifest root must be a JSON object.");
            }

            var propertyNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!propertyNames.Add(property.Name))
                {
                    return new MarseyManifestIssue(
                        "duplicate-property",
                        $"The manifest contains duplicate property '{property.Name}'.");
                }
            }

            return null;
        }
        catch (JsonException exception)
        {
            return new MarseyManifestIssue("invalid-json", exception.Message);
        }
        finally
        {
            data.Position = 0;
        }
    }

    private static MemoryStream? ReadBounded(Stream stream)
    {
        if (stream.CanSeek && stream.Length - stream.Position > MaximumManifestBytes)
            return null;

        var data = new MemoryStream();
        var buffer = new byte[8192];
        var total = 0;

        while (true)
        {
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0)
                break;

            total += read;
            if (total > MaximumManifestBytes)
            {
                data.Dispose();
                return null;
            }

            data.Write(buffer, 0, read);
        }

        data.Position = 0;
        return data;
    }

    private static void ValidateIdentifier(
        string? value,
        string field,
        ICollection<MarseyManifestIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(new MarseyManifestIssue("missing-field", $"{field} is required."));
            return;
        }

        if (!IdentifierPattern.IsMatch(value))
        {
            issues.Add(new MarseyManifestIssue(
                "invalid-identifier",
                $"{field} must contain 3-128 lowercase ASCII letters, digits, dots, underscores, or hyphens."));
        }
    }

    private static void ValidateRequiredText(
        string? value,
        string field,
        int maximumLength,
        ICollection<MarseyManifestIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(new MarseyManifestIssue("missing-field", $"{field} is required."));
            return;
        }

        if (value.Length > maximumLength)
        {
            issues.Add(new MarseyManifestIssue(
                "field-too-long",
                $"{field} cannot exceed {maximumLength} characters."));
        }

        if (value.Any(char.IsControl))
        {
            issues.Add(new MarseyManifestIssue(
                "control-character",
                $"{field} cannot contain control characters."));
        }
    }

    private static void ValidateEntryAssembly(
        string? entryAssembly,
        ICollection<MarseyManifestIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(entryAssembly))
        {
            issues.Add(new MarseyManifestIssue("missing-field", "entryAssembly is required."));
            return;
        }

        if (entryAssembly.Length > 255 ||
            Path.IsPathRooted(entryAssembly) ||
            entryAssembly.Contains('/') ||
            entryAssembly.Contains('\\') ||
            entryAssembly.Contains(':') ||
            entryAssembly.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            Path.GetFileName(entryAssembly) != entryAssembly)
        {
            issues.Add(new MarseyManifestIssue(
                "invalid-entry-assembly-path",
                "entryAssembly must be a valid file name inside the mod directory, not a path."));
            return;
        }

        if (!string.Equals(Path.GetExtension(entryAssembly), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new MarseyManifestIssue(
                "invalid-entry-assembly-extension",
                "entryAssembly must reference a .dll file."));
        }
    }

    private static Version? ParseOptionalVersion(
        string? value,
        string field,
        ICollection<MarseyManifestIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (Version.TryParse(value, out var version))
            return version;

        issues.Add(new MarseyManifestIssue("invalid-version", $"{field} must be a valid dotted version."));
        return null;
    }

    private static string[] NormalizeReferences(
        IReadOnlyList<string>? values,
        string field,
        ICollection<MarseyManifestIssue> issues)
    {
        if (values is null || values.Count == 0)
            return Array.Empty<string>();

        var normalized = new List<string>(values.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var value in values)
        {
            var previousIssueCount = issues.Count;
            ValidateIdentifier(value, field, issues);
            if (issues.Count != previousIssueCount)
                continue;

            if (!seen.Add(value))
            {
                issues.Add(new MarseyManifestIssue(
                    "duplicate-reference",
                    $"{field} contains duplicate ID '{value}'."));
                continue;
            }

            normalized.Add(value);
        }

        return normalized.ToArray();
    }

    private static MarseyManifestReadResult Invalid(string code, string message)
    {
        return new MarseyManifestReadResult(
            null,
            new[] { new MarseyManifestIssue(code, message) });
    }

    private sealed class ManifestDocument
    {
        [JsonPropertyName("$schema")]
        public string? Schema { get; init; }

        public string? Id { get; init; }
        public string? Name { get; init; }
        public string? Version { get; init; }
        public int? ApiVersion { get; init; }
        public string? EntryAssembly { get; init; }
        public string? EntryType { get; init; }
        public string? MinimumLauncherVersion { get; init; }
        public string? MaximumLauncherVersionExclusive { get; init; }
        public MarseyLoadPhase? LoadPhase { get; init; }
        public string[]? Dependencies { get; init; }
        public string[]? Conflicts { get; init; }
    }
}
