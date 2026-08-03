# Launcher extension architecture

This document defines the extension boundary used to integrate optional launcher functionality without coupling it to the launcher's update and content-integrity paths.

## Project boundaries

The first migration slice is split into three assemblies:

- `SS14.Launcher.Extensibility` owns neutral process-lifetime contracts and the extension host;
- `Marsey.Core` owns untrusted manifest parsing, compatibility evaluation, and file discovery;
- `Marsey.LauncherIntegration` adapts the safe Marsey catalog to the launcher lifecycle.

`Marsey.Core` does not reference the launcher. `Marsey.LauncherIntegration` references only the neutral extension contracts and `Marsey.Core`. This prevents a dependency cycle and keeps Marsey-specific behavior out of the launcher update subsystem.

## Current lifecycle

`LauncherExtensionHost` provides process-lifetime registration:

- extensions register before Avalonia startup initialization;
- extensions initialize after localization, launcher metadata, content storage, and override assets are initialized;
- extensions initialize in registration order;
- successfully initialized extensions shut down in reverse registration order;
- one extension failure is logged and isolated from the base launcher and other extensions;
- registration is closed once initialization begins.

The launcher currently registers `MarseyLauncherExtension`. At this stage it only creates the Marsey mod directory, reads `marsey.json` files, validates metadata, and builds a compatibility catalog. It does **not** load or execute declared DLL files.

## Hard boundaries

Extensions must not be called from, or become dependencies of:

- `Updater` and its manifest/ZIP download paths;
- content hash verification;
- content database migrations or garbage collection;
- engine/module signature and compatibility verification;
- authentication token storage;
- launcher self-update and bootstrap code.

Keeping those paths extension-free preserves upstream behavior and makes future upstream synchronization tractable.

## Registration

The host and concrete integrations are registered during application construction:

```csharp
var extensionHost = new LauncherExtensionHost();
var marsey = new MarseyLauncherExtension(modsDirectory, launcherVersion);

extensionHost.Register(marsey);
locator.RegisterConstant(extensionHost);
```

Registration must happen before `App.OnStartup` invokes `InitializeAll()`.

## Current security properties

The manifest/discovery stage:

- caps manifest size at 256 KiB;
- requires string enum values and rejects numeric enum coercion;
- rejects absolute paths, directory traversal, and entry assemblies outside the mod directory;
- rejects symbolic-link/reparse-point mod directories, manifests, and entry assemblies;
- validates stable lowercase identifiers, versions, API compatibility, conflicts, and dependencies;
- enumerates only the root manifest and direct child mod directories;
- never loads an assembly during discovery.

These checks reduce accidental and malicious ambiguity, but they do not make future in-process Harmony mods sandboxed. Any DLL eventually loaded into the game process must still be treated as fully trusted code.

## Planned evolution

The next migration slices should add narrowly scoped contracts rather than exposing launcher internals wholesale:

1. dependency resolution and deterministic load ordering;
2. profile, enable/disable, and last-known-good state;
3. an immutable pre-launch context and explicit allow/block validation result;
4. post-client-exit diagnostics and quarantine;
5. localized UI for the catalog and compatibility errors;
6. an opt-in runtime host for compatible trusted mods.

Harmony or other in-process patching must remain outside the base launcher project. The base launcher should depend only on neutral contracts, while the optional integration assembly owns discovery and any later execution policy.
