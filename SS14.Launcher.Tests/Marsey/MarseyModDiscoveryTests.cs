using System;
using System.IO;
using System.Linq;
using Marsey.Core.Discovery;
using NUnit.Framework;

namespace SS14.Launcher.Tests.Marsey;

[TestFixture]
public sealed class MarseyModDiscoveryTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"marsey-discovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void DiscoversModsInDeterministicIdentifierOrder()
    {
        CreateMod("z-mod", "community.z-mod");
        CreateMod("a-mod", "community.a-mod");

        var result = new MarseyModDiscovery().Discover(_root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Issues, Is.Empty);
            Assert.That(
                result.Mods.Select(candidate => candidate.Manifest.Id),
                Is.EqualTo(new[] { "community.a-mod", "community.z-mod" }));
        });
    }

    [Test]
    public void ReportsMissingEntryAssemblyWithoutLoadingCode()
    {
        var directory = Path.Combine(_root, "missing-assembly");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, MarseyModDiscovery.ManifestFileName),
            CreateManifest("community.missing-assembly", "Missing.dll"));

        var result = new MarseyModDiscovery().Discover(_root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Mods, Is.Empty);
            Assert.That(result.Issues.Any(issue => issue.Code == "entry-assembly-missing"), Is.True);
        });
    }

    [Test]
    public void ReportsDuplicateIdentifiers()
    {
        CreateMod("first", "community.duplicate");
        CreateMod("second", "community.duplicate");

        var result = new MarseyModDiscovery().Discover(_root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Mods, Has.Count.EqualTo(1));
            Assert.That(result.Issues.Any(issue => issue.Code == "duplicate-mod-id"), Is.True);
        });
    }

    [Test]
    public void MissingRootReturnsEmptyResult()
    {
        var missingRoot = Path.Combine(_root, "does-not-exist");

        var result = new MarseyModDiscovery().Discover(missingRoot);

        Assert.Multiple(() =>
        {
            Assert.That(result.Mods, Is.Empty);
            Assert.That(result.Issues, Is.Empty);
        });
    }

    [Test]
    public void RejectsSymbolicLinkRoot()
    {
        var target = Path.Combine(_root, "target");
        var link = Path.Combine(_root, "root-link");
        Directory.CreateDirectory(target);

        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (
            exception is PlatformNotSupportedException or UnauthorizedAccessException or IOException)
        {
            Assert.Ignore($"Symbolic links are unavailable in this test environment: {exception.Message}");
            return;
        }

        try
        {
            var result = new MarseyModDiscovery().Discover(link);

            Assert.Multiple(() =>
            {
                Assert.That(result.Mods, Is.Empty);
                Assert.That(result.Issues.Any(issue => issue.Code == "mod-root-reparse-point"), Is.True);
            });
        }
        finally
        {
            if (Directory.Exists(link))
                Directory.Delete(link);
        }
    }

    private void CreateMod(string directoryName, string id)
    {
        var directory = Path.Combine(_root, directoryName);
        Directory.CreateDirectory(directory);

        File.WriteAllText(
            Path.Combine(directory, MarseyModDiscovery.ManifestFileName),
            CreateManifest(id, "Entry.dll"));
        File.WriteAllBytes(Path.Combine(directory, "Entry.dll"), Array.Empty<byte>());
    }

    private static string CreateManifest(string id, string entryAssembly)
    {
        return $$"""
                 {
                   "id": "{{id}}",
                   "name": "Test Mod",
                   "version": "1.0.0",
                   "apiVersion": 1,
                   "entryAssembly": "{{entryAssembly}}",
                   "entryType": "TestMod.EntryPoint"
                 }
                 """;
    }
}
