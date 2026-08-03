using System;
using System.IO;
using System.Text;
using Marsey.Core.Compatibility;
using Marsey.Core.Manifests;
using NUnit.Framework;

namespace SS14.Launcher.Tests.Marsey;

[TestFixture]
public sealed class MarseyManifestReaderTests
{
    private readonly MarseyManifestReader _reader = new();

    [Test]
    public void ReadsValidManifest()
    {
        var result = Read(
            """
            {
              "id": "community.example-mod",
              "name": "Example Mod",
              "version": "1.2.3",
              "apiVersion": 1,
              "entryAssembly": "ExampleMod.dll",
              "entryType": "ExampleMod.EntryPoint",
              "minimumLauncherVersion": "0.39.1",
              "maximumLauncherVersionExclusive": "0.40.0",
              "loadPhase": "AfterContentAssemblies",
              "dependencies": ["community.library"],
              "conflicts": ["community.legacy-mod"]
            }
            """);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Manifest, Is.Not.Null);
            Assert.That(result.Manifest!.Id, Is.EqualTo("community.example-mod"));
            Assert.That(result.Manifest.Version, Is.EqualTo(new Version(1, 2, 3)));
            Assert.That(result.Manifest.EntryAssembly, Is.EqualTo("ExampleMod.dll"));
            Assert.That(result.Manifest.LoadPhase, Is.EqualTo(MarseyLoadPhase.AfterContentAssemblies));
        });
    }

    [TestCase("../ExampleMod.dll")]
    [TestCase("folder/ExampleMod.dll")]
    [TestCase("folder\\ExampleMod.dll")]
    [TestCase("C:\\ExampleMod.dll")]
    public void RejectsEntryAssemblyPaths(string entryAssembly)
    {
        var result = Read(CreateManifest(entryAssembly));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues, Has.Some.Matches<MarseyManifestIssue>(
                issue => issue.Code == "invalid-entry-assembly-path"));
        });
    }

    [Test]
    public void RejectsInvalidLauncherRange()
    {
        var result = Read(
            """
            {
              "id": "community.example-mod",
              "name": "Example Mod",
              "version": "1.0.0",
              "apiVersion": 1,
              "entryAssembly": "ExampleMod.dll",
              "entryType": "ExampleMod.EntryPoint",
              "minimumLauncherVersion": "0.40.0",
              "maximumLauncherVersionExclusive": "0.39.1"
            }
            """);

        Assert.That(result.Issues, Has.Some.Matches<MarseyManifestIssue>(
            issue => issue.Code == "invalid-launcher-range"));
    }

    [Test]
    public void RejectsDependencyConflictOverlap()
    {
        var result = Read(
            """
            {
              "id": "community.example-mod",
              "name": "Example Mod",
              "version": "1.0.0",
              "apiVersion": 1,
              "entryAssembly": "ExampleMod.dll",
              "entryType": "ExampleMod.EntryPoint",
              "dependencies": ["community.shared"],
              "conflicts": ["community.shared"]
            }
            """);

        Assert.That(result.Issues, Has.Some.Matches<MarseyManifestIssue>(
            issue => issue.Code == "dependency-conflict-overlap"));
    }

    [TestCase(1, "0.39.1", MarseyCompatibilityStatus.Compatible)]
    [TestCase(2, "0.39.1", MarseyCompatibilityStatus.UnsupportedApiVersion)]
    [TestCase(1, "0.39.0", MarseyCompatibilityStatus.LauncherTooOld)]
    [TestCase(1, "0.40.0", MarseyCompatibilityStatus.LauncherTooNew)]
    public void EvaluatesLauncherCompatibility(
        int supportedApiVersion,
        string launcherVersion,
        MarseyCompatibilityStatus expectedStatus)
    {
        var result = Read(
            """
            {
              "id": "community.example-mod",
              "name": "Example Mod",
              "version": "1.0.0",
              "apiVersion": 1,
              "entryAssembly": "ExampleMod.dll",
              "entryType": "ExampleMod.EntryPoint",
              "minimumLauncherVersion": "0.39.1",
              "maximumLauncherVersionExclusive": "0.40.0"
            }
            """);

        var compatibility = new MarseyCompatibilityEvaluator().Evaluate(
            result.Manifest!,
            supportedApiVersion,
            Version.Parse(launcherVersion));

        Assert.That(compatibility.Status, Is.EqualTo(expectedStatus));
    }

    private MarseyManifestReadResult Read(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return _reader.Read(stream);
    }

    private static string CreateManifest(string entryAssembly)
    {
        return $$"""
                 {
                   "id": "community.example-mod",
                   "name": "Example Mod",
                   "version": "1.0.0",
                   "apiVersion": 1,
                   "entryAssembly": "{{entryAssembly.Replace("\\", "\\\\")}}",
                   "entryType": "ExampleMod.EntryPoint"
                 }
                 """;
    }
}
