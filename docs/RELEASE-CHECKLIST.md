# Release acceptance

Source preview and binary distribution have separate gates. The maintainer authorized public source visibility after review on 2026-10-27. This does not approve an installable or production-ready release.

## Public Alpha source preview

- [x] Review README, LICENSE, NOTICE and upstream attribution.
- [x] Review tracked files, the custom commit's author metadata and the upstream ancestry; inspect the layout screenshot for personal information.
- [x] Scan all 462 tracked regular files for credential patterns and exact current private profile values; no matches found. This is not a comprehensive security audit.
- [x] Confirm no private runtime database, node configuration, logs or backups are tracked.
- [x] Rebuild in fresh Windows CI with recursive submodules and the documented SDK.
- [x] Review successful Windows build and 103 offline checks for code commit `a8c8ade6a364fc2588184fa319a0c91f27ab4207`.
- [x] Document incomplete live acceptance tests and publish source only, without binary assets.

See [verification evidence](VERIFICATION.md).

## Before distributing binaries

- [ ] Recheck README, LICENSE, NOTICE and upstream attribution for the release contents.
- [ ] Review changes since the source preview for personal information or secrets; separately inspect screenshots and release archives.
- [ ] Verify no config databases, node/subscription links, UUIDs/passwords, private keys, local logs or runtime backups are bundled.
- [ ] Rebuild from a fresh recursive clone using the documented SDK.
- [ ] Review successful Windows CI build and offline regression checks.
- [ ] Test on a clean Windows user account with empty profiles; first run must not connect or select personal applications.
- [ ] Verify repeated manual opens, different path casing and restart handoff retain one GUI.
- [ ] Verify GUI connect→disconnect, no owned core processes/proxy listeners/TUN after OFF, and OFF persistence across a true reboot/logon.
- [ ] Verify Edge's three modes, selected/unselected processes, local networks and DNS/IPv6 behavior.
- [ ] Verify exit/uninstall/network recovery and coexistence with other proxy software.
- [ ] Check licenses, corresponding sources and notices for every bundled dependency, core and data file.
- [ ] Prepare a versioned, credential-free release archive and SHA256; record signature status honestly.
- [ ] Publish release notes with supported OS, known limits and exact source tag. Do not use an upstream binary version as the custom release version.

The workflow is read-only and does not publish releases or access signing secrets. Archived upstream workflow files in docs/upstream-ci are historical references, not active workflows.
