# Source preview verification

Reviewed on 2026-10-27 for public Alpha source visibility. Code under review: `a8c8ade6a364fc2588184fa319a0c91f27ab4207`, based on upstream v2rayN 7.25.6. Later documentation updates do not change this code baseline.

## Completed

- Fresh GitHub-hosted Windows checkout, recursive submodule initialization and .NET SDK 10.0.402 setup succeeded.
- Windows desktop application and helper publish succeeded.
- 79 routing, presentation and diagnostic checks passed.
- 24 single-instance identity, forwarding, timeout, release and handoff checks passed.
- Evidence: [first successful workflow](https://github.com/91275979/chloride-vpn/actions/runs/40353209516), [second successful workflow](https://github.com/91275979/chloride-vpn/actions/runs/40353900503). These checks use generated test configurations and do not probe a personal node.
- All 462 tracked regular files were checked for high-confidence private-key/token patterns, local personal paths, forbidden runtime files and exact current private profile values. No matches were found. The added history consists of one custom commit on the retained public upstream ancestry; the custom author uses a GitHub noreply address. The layout image contains example data and no node credentials.
- GPL version 3 license, upstream attribution and source history are retained. No personal runtime configuration or core binaries are included in the source preview.

## Still unverified for this public source build

A clean Windows user's first run, a complete GUI connection/disconnection cycle, saved OFF across a real reboot/logon, Edge and application routing on real traffic, DNS/IPv6 leaks, long-duration stability, uninstall recovery and coexistence with other proxy software still require acceptance testing. Offline assertions and builds cannot establish these properties.

Local test DLL execution was blocked by Windows application-control policy; no protection was disabled or bypassed. The successful CI checks independently rebuilt and executed the tests.

This evidence supports an explicitly labeled source preview, not a security certification, an anonymity guarantee or a production binary release. See [known limits](KNOWN-ISSUES.md) and the [binary release checklist](RELEASE-CHECKLIST.md).
