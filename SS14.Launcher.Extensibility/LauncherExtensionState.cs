using System;

namespace SS14.Launcher.Extensibility;

/// <summary>
/// Current lifecycle state of a launcher extension.
/// </summary>
public enum LauncherExtensionState
{
    Registered,
    Initialized,
    InitializationFailed,
    Stopped,
    ShutdownFailed
}

/// <summary>
/// Immutable diagnostic snapshot for a registered launcher extension.
/// </summary>
public sealed record LauncherExtensionInfo(
    string Id,
    LauncherExtensionState State,
    Exception? Error);
