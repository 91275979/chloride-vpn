# Release acceptance

Keep the repository private until the maintainer approves public visibility after reviewing these items.

- [ ] Review README, LICENSE, NOTICE and upstream attribution.
- [ ] Review the exact committed files and full history for personal information or secrets; separately inspect screenshots and release archives.
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
