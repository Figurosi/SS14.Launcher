# Marsey mod manifest

Each mod is stored in its own direct child directory under the launcher Marsey mod directory and must contain:

- `marsey.json` — declarative metadata;
- the entry assembly named by `entryAssembly`.

The initial migration stage validates and catalogs these files but does not execute the assembly.

## Example

```json
{
  "$schema": "https://raw.githubusercontent.com/Figurosi/SS14.Launcher/master/Documentation/marsey.schema.json",
  "id": "community.example-mod",
  "name": "Example Mod",
  "version": "1.2.3",
  "apiVersion": 1,
  "entryAssembly": "ExampleMod.dll",
  "entryType": "ExampleMod.EntryPoint",
  "minimumLauncherVersion": "0.39.1",
  "maximumLauncherVersionExclusive": "0.40.0",
  "loadPhase": "AfterContentAssemblies",
  "dependencies": [
    "community.shared-library"
  ],
  "conflicts": [
    "community.legacy-example"
  ]
}
```

## Fields

### `id`

Required stable identifier. It must be 3–128 characters and contain only lowercase ASCII letters, digits, `.`, `_`, or `-`. It must not change between releases of the same mod.

### `name`

Required display name, at most 128 characters. Control characters are rejected.

### `version`

Required dotted `System.Version` value used for diagnostics and future dependency resolution.

### `apiVersion`

Required positive integer. The first safe catalog API is version `1`. A mod with a different API version is cataloged as incompatible and must not be executed.

### `entryAssembly`

Required `.dll` file name in the same directory as `marsey.json`. Paths, absolute names, `..`, directory separators, drive prefixes, and symbolic links/reparse points are rejected.

### `entryType`

Required fully qualified entry-point type name, at most 512 characters. The current migration stage stores this value but does not instantiate it.

### `minimumLauncherVersion`

Optional inclusive minimum launcher version.

### `maximumLauncherVersionExclusive`

Optional exclusive upper launcher version. When both bounds are supplied, the minimum must be lower than the maximum.

### `loadPhase`

Optional string enum. Defaults to `AfterContentAssemblies`.

Allowed values:

- `BeforeContentAssemblies`;
- `AfterContentAssemblies`;
- `AfterClientStartup`.

Numeric enum values are rejected.

### `dependencies`

Optional unique list of required mod IDs. A mod cannot depend on itself, and the same ID cannot appear in both `dependencies` and `conflicts`.

### `conflicts`

Optional unique list of incompatible mod IDs. A mod cannot conflict with itself.

## Discovery rules

The launcher checks:

1. `marsey.json` directly inside the configured mod root, if present;
2. `marsey.json` inside each direct child directory.

Discovery is deliberately not recursive. Mod directories, manifests, and entry assemblies that are symbolic links or reparse points are rejected. Manifest files are limited to 256 KiB. Unknown fields are rejected to catch spelling mistakes and unsupported metadata early.

## Trust model

A valid manifest means only that the package is structurally well formed and compatible with the declared launcher/API range. It does not prove that the DLL is safe. Any future in-process mod execution must be an explicit opt-in and must describe the code as fully trusted, unsandboxed access to the game process.
