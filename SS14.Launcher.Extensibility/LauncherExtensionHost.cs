using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace SS14.Launcher.Extensibility;

/// <summary>
/// Owns registration and process-lifetime callbacks for launcher extensions.
/// </summary>
/// <remarks>
/// Extension failures are isolated so that an optional extension cannot prevent the base launcher from starting
/// or shutting down. The host intentionally provides lifecycle only; update and content-database code must not
/// depend on extensions.
/// </remarks>
public sealed class LauncherExtensionHost
{
    private readonly object _lock = new();
    private readonly List<ExtensionEntry> _extensions = new();

    private HostState _state = HostState.Registering;

    /// <summary>
    /// Returns a point-in-time diagnostic snapshot of registered extensions.
    /// </summary>
    public IReadOnlyList<LauncherExtensionInfo> Extensions
    {
        get
        {
            lock (_lock)
            {
                return _extensions
                    .Select(entry => entry.ToInfo())
                    .ToArray();
            }
        }
    }

    /// <summary>
    /// Registers an extension before launcher extension initialization begins.
    /// </summary>
    public void Register(ILauncherExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        if (string.IsNullOrWhiteSpace(extension.Id))
            throw new ArgumentException("Launcher extensions must have a non-empty identifier.", nameof(extension));

        lock (_lock)
        {
            if (_state != HostState.Registering)
                throw new InvalidOperationException("Launcher extensions can only be registered before initialization.");

            if (_extensions.Any(entry => string.Equals(entry.Extension.Id, extension.Id, StringComparison.Ordinal)))
                throw new InvalidOperationException($"A launcher extension with ID '{extension.Id}' is already registered.");

            _extensions.Add(new ExtensionEntry(extension));
        }
    }

    /// <summary>
    /// Initializes all registered extensions in registration order.
    /// </summary>
    public void InitializeAll()
    {
        ExtensionEntry[] entries;

        lock (_lock)
        {
            switch (_state)
            {
                case HostState.Initialized:
                    return;
                case HostState.Registering:
                    _state = HostState.Initializing;
                    entries = _extensions.ToArray();
                    break;
                case HostState.Initializing:
                    throw new InvalidOperationException("Launcher extension initialization is already in progress.");
                case HostState.ShuttingDown:
                case HostState.Shutdown:
                    throw new InvalidOperationException("Launcher extension host has already begun shutting down.");
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        foreach (var entry in entries)
        {
            try
            {
                Log.Information("Initializing launcher extension {ExtensionId}", entry.Extension.Id);
                entry.Extension.Initialize();
                UpdateEntry(entry, LauncherExtensionState.Initialized, null);
            }
            catch (Exception exception)
            {
                UpdateEntry(entry, LauncherExtensionState.InitializationFailed, exception);
                Log.Error(exception, "Launcher extension {ExtensionId} failed to initialize", entry.Extension.Id);
            }
        }

        lock (_lock)
        {
            _state = HostState.Initialized;
        }
    }

    /// <summary>
    /// Shuts down successfully initialized extensions in reverse registration order.
    /// </summary>
    public void ShutdownAll()
    {
        ExtensionEntry[] entries;

        lock (_lock)
        {
            switch (_state)
            {
                case HostState.Shutdown:
                case HostState.ShuttingDown:
                    return;
                case HostState.Registering:
                    _state = HostState.Shutdown;
                    return;
                case HostState.Initializing:
                    throw new InvalidOperationException(
                        "Launcher extensions cannot be shut down while initialization is in progress.");
                case HostState.Initialized:
                    _state = HostState.ShuttingDown;
                    entries = _extensions
                        .Where(entry => entry.State == LauncherExtensionState.Initialized)
                        .Reverse()
                        .ToArray();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        foreach (var entry in entries)
        {
            try
            {
                Log.Information("Shutting down launcher extension {ExtensionId}", entry.Extension.Id);
                entry.Extension.Shutdown();
                UpdateEntry(entry, LauncherExtensionState.Stopped, null);
            }
            catch (Exception exception)
            {
                UpdateEntry(entry, LauncherExtensionState.ShutdownFailed, exception);
                Log.Error(exception, "Launcher extension {ExtensionId} failed to shut down", entry.Extension.Id);
            }
        }

        lock (_lock)
        {
            _state = HostState.Shutdown;
        }
    }

    private void UpdateEntry(
        ExtensionEntry entry,
        LauncherExtensionState state,
        Exception? error)
    {
        lock (_lock)
        {
            entry.State = state;
            entry.Error = error;
        }
    }

    private sealed class ExtensionEntry(ILauncherExtension extension)
    {
        public ILauncherExtension Extension { get; } = extension;
        public LauncherExtensionState State { get; set; } = LauncherExtensionState.Registered;
        public Exception? Error { get; set; }

        public LauncherExtensionInfo ToInfo()
        {
            return new LauncherExtensionInfo(Extension.Id, State, Error);
        }
    }

    private enum HostState
    {
        Registering,
        Initializing,
        Initialized,
        ShuttingDown,
        Shutdown
    }
}
