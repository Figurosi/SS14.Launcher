namespace SS14.Launcher.Extensibility;

/// <summary>
/// Defines a launcher extension that participates in the launcher process lifetime.
/// </summary>
/// <remarks>
/// Extensions are initialized after the launcher's core services and content database are ready.
/// They are shut down in reverse registration order when the launcher exits.
/// </remarks>
public interface ILauncherExtension
{
    /// <summary>
    /// Stable, globally unique identifier for the extension.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Initializes the extension.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Releases resources owned by the extension.
    /// </summary>
    void Shutdown();
}
