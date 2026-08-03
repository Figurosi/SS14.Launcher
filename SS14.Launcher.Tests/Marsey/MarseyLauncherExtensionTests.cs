using System;
using System.IO;
using System.Linq;
using Marsey.Core.Compatibility;
using Marsey.Core.Discovery;
using Marsey.LauncherIntegration;
using NUnit.Framework;

namespace SS14.Launcher.Tests.Marsey;

[TestFixture]
public sealed class MarseyLauncherExtensionTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"marsey-extension-{Guid.NewGuid():N}");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void CreatesModDirectoryInsideIsolatedInitialization()
    {
        var extension = new MarseyLauncherExtension(_root, new Version(0, 39, 1));

        extension.Initialize();

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(_root), Is.True);
            Assert.That(extension.Catalog.Entries, Is.Empty);
            Assert.That(extension.Catalog.Issues, Is.Empty);
        });
    }

    [Test]
    public void BuildsCompatibilityCatalogWithoutLoadingAssemblies()
    {
        Directory.CreateDirectory(_root);
        CreateMod("compatible", "community.compatible", apiVersion: 1);
        CreateMod("future-api", "community.future-api", apiVersion: 2);

        var extension = new MarseyLauncherExtension(_root, new Version(0, 39, 1));
        extension.Initialize();

        var compatible = extension.Catalog.Entries.Single(entry => entry.Candidate.Manifest.Id == "community.compatible");
        var futureApi = extension.Catalog.Entries.Single(entry => entry.Candidate.Manifest.Id == "community.future-api");

        Assert.Multiple(() =>
        {
            Assert.That(extension.Catalog.Issues, Is.Empty);
            Assert.That(compatible.Compatibility.Status, Is.EqualTo(MarseyCompatibilityStatus.Compatible));
            Assert.That(futureApi.Compatibility.Status, Is.EqualTo(MarseyCompatibilityStatus.UnsupportedApiVersion));
        });
    }

    private void CreateMod(string directoryName, string id, int apiVersion)
    {
        var directory = Path.Combine(_root, directoryName);
        Directory.CreateDirectory(directory);

        File.WriteAllText(
            Path.Combine(directory, MarseyModDiscovery.ManifestFileName),
            $$"""
              {
                "id": "{{id}}",
                "name": "Test Mod",
                "version": "1.0.0",
                "apiVersion": {{apiVersion}},
                "entryAssembly": "Entry.dll",
                "entryType": "TestMod.EntryPoint",
                "minimumLauncherVersion": "0.39.1",
                "maximumLauncherVersionExclusive": "0.40.0"
              }
              """);
        File.WriteAllBytes(Path.Combine(directory, "Entry.dll"), Array.Empty<byte>());
    }
}
