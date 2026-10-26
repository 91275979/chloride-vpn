# Build and checks

Requirements: Windows x64, Git with submodule support, .NET SDK 10.0.402 and access to NuGet. Source is pinned through global.json and Directory.Packages.props.

```powershell
git clone --recurse-submodules https://github.com/91275979/chloride-vpn.git
cd chloride-vpn
pwsh -File scripts/Build.ps1
pwsh -File scripts/Test.ps1
```

For the private preview, the account running Git needs access to the repository. To initialize a previously cloned checkout:

```powershell
git submodule update --init --recursive
```

Build output: artifacts/win-x64. The script builds the WPF app and the inherited helper, includes COPYING/NOTICE/a launcher, writes a build hash and refuses output containing runtime-state directories. It neither installs a system SDK, imports profiles nor launches a VPN.

This output is application-only. Supply separately verified compatible runtime cores and rule data before using a node; no private runtime folder should be copied into it. Check licensing and source correspondence before bundling third-party binaries for a release.

The check script executes managed console checks for routing, connection presentation, bounded diagnostic parsing/download behavior, first-run defaults and Windows kernel-object single-instance semantics. It does not use a real node, request active IP reputation or run a live speed probe.

An optional development probe exists in the diagnostic harness and requires an explicitly running local node proxy; it is not used by CI. Do not run it against somebody else's node configuration.
