# Launcher extension architecture

This document defines the initial extension boundary used to integrate optional launcher functionality without coupling it to the launcher's update and content-integrity paths.

## Current scope

`LauncherExtensionHost` provides process-lifetime registration only:

- extensions register before Avalonia startup initialization;
- extensions initialize after localization, launcher metadata, content storage, and override assets are initialized;
- extensions initialize in registration order;
- successfully initialized extensions shut down in reverse registration order;
- one extension failure is logged and isolated from the base launcher and other extensions;
- registration is closed once initialization begins.

The host is intentionally empty in the base launcher. Adding the host therefore does not alter normal launcher behavior.

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

The host is registered in the launcher service locator during application construction:

```csharp
var host = Locator.Current.GetRequiredService<LauncherExtensionHost>();
host.Register(new ExampleExtension());
```

Registration must happen before `App.OnStartup` invokes `InitializeAll()`.

## Planned evolution

The next migration slices should add narrowly scoped contracts rather than exposing launcher internals wholesale:

1. immutable launcher environment and diagnostics context;
2. pre-launch validation with an explicit allow/block result;
3. post-client-exit diagnostics;
4. extension metadata, compatibility ranges, and deterministic load ordering;
5. profile and safe-mode support;
6. a dedicated Marsey integration project that implements these contracts.

Harmony or other in-process patching must remain outside the base launcher project. The base launcher should depend only on neutral contracts, while an optional integration assembly owns patch discovery and execution.
