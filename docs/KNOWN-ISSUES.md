# Alpha scope and known limits

Current target: Windows x64, source version 0.1.0-alpha.1, based on v2rayN 7.25.6. Other upstream platforms remain in the source tree but are not customized or supported by this preview.

- A real Windows reboot/logon, complete GUI connect→disconnect cycle, long-duration use, DNS/IPv6 leak checks and clean uninstall/network restoration still require release acceptance testing. Existing local checks are not a substitute.
- The UI process currently requires administrator permission. Manual opens may show normal Windows UAC. No security-policy bypass is provided.
- Single-instance identity is scoped to a user/session and a normalized executable path. It does not merge separately copied installations or resolve junction/hardlink aliases. Run one installation at a time.
- IP quality is single-source IPQuery data. Missing fields remain unknown; data may be inaccurate or temporarily unavailable.
- Speed is a bounded download sample, affected by the endpoint and current route; it is not the maximum bandwidth or every website's performance. Active probes are manual.
- The custom icon is embedded for the application/window. Some inherited tray-state assets remain from upstream.
- Build output is application-only. No runtime cores, rule-data bundle, personal profile, signed installer or public binary release is supplied yet.
- Some local regression test binaries have been blocked by Windows application-control policy. The CI check must be reviewed independently; do not disable OS protection to make a test pass.
- Core and dependency source/license/notice verification, package signing and full release acceptance are required before public binary distribution.

The connection switch persists OFF and gates main/pre-core loading; opening or automatically starting the GUI does not override a saved OFF choice. A separate application's network connection is outside Chloride's switch.
