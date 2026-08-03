using System;
using System.Collections.Generic;
using NUnit.Framework;
using SS14.Launcher.Extensibility;

namespace SS14.Launcher.Tests.Extensibility;

[TestFixture]
public sealed class LauncherExtensionHostTests
{
    [Test]
    public void InitializesInRegistrationOrderAndShutsDownInReverseOrder()
    {
        var events = new List<string>();
        var host = new LauncherExtensionHost();

        host.Register(new TestExtension("first", events));
        host.Register(new TestExtension("second", events));

        host.InitializeAll();
        host.ShutdownAll();

        Assert.That(events, Is.EqualTo(new[]
        {
            "initialize:first",
            "initialize:second",
            "shutdown:second",
            "shutdown:first"
        }));
    }

    [Test]
    public void RejectsDuplicateIdentifiers()
    {
        var host = new LauncherExtensionHost();
        host.Register(new TestExtension("duplicate"));

        Assert.That(
            () => host.Register(new TestExtension("duplicate")),
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void RejectsRegistrationAfterInitialization()
    {
        var host = new LauncherExtensionHost();
        host.InitializeAll();

        Assert.That(
            () => host.Register(new TestExtension("late")),
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void IsolatesInitializationFailure()
    {
        var events = new List<string>();
        var host = new LauncherExtensionHost();

        host.Register(new TestExtension("broken", events, failInitialization: true));
        host.Register(new TestExtension("healthy", events));

        host.InitializeAll();
        host.ShutdownAll();

        Assert.Multiple(() =>
        {
            Assert.That(events, Is.EqualTo(new[]
            {
                "initialize:broken",
                "initialize:healthy",
                "shutdown:healthy"
            }));
            Assert.That(host.Extensions[0].State, Is.EqualTo(LauncherExtensionState.InitializationFailed));
            Assert.That(host.Extensions[0].Error, Is.TypeOf<InvalidOperationException>());
            Assert.That(host.Extensions[1].State, Is.EqualTo(LauncherExtensionState.Stopped));
        });
    }

    [Test]
    public void ShutdownIsIdempotent()
    {
        var events = new List<string>();
        var host = new LauncherExtensionHost();
        host.Register(new TestExtension("extension", events));

        host.InitializeAll();
        host.ShutdownAll();
        host.ShutdownAll();

        Assert.That(events, Is.EqualTo(new[]
        {
            "initialize:extension",
            "shutdown:extension"
        }));
    }

    private sealed class TestExtension : ILauncherExtension
    {
        private readonly List<string>? _events;
        private readonly bool _failInitialization;

        public TestExtension(
            string id,
            List<string>? events = null,
            bool failInitialization = false)
        {
            Id = id;
            _events = events;
            _failInitialization = failInitialization;
        }

        public string Id { get; }

        public void Initialize()
        {
            _events?.Add($"initialize:{Id}");

            if (_failInitialization)
                throw new InvalidOperationException("Initialization failure for test.");
        }

        public void Shutdown()
        {
            _events?.Add($"shutdown:{Id}");
        }
    }
}
