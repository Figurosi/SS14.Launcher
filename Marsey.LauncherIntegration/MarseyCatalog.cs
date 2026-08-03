using System.Collections.Generic;
using Marsey.Core.Compatibility;
using Marsey.Core.Discovery;

namespace Marsey.LauncherIntegration;

public sealed record MarseyCatalogEntry(
    MarseyModCandidate Candidate,
    MarseyCompatibilityResult Compatibility);

public sealed record MarseyCatalog(
    IReadOnlyList<MarseyCatalogEntry> Entries,
    IReadOnlyList<MarseyDiscoveryIssue> Issues);
