# Security and privacy

This is an early Windows desktop preview. Do not treat an Alpha build as a security audit or a guarantee against traffic/DNS/IPv6 leaks.

- Never attach node links, subscription URLs, UUIDs/passwords, private keys, API tokens, configuration databases or unredacted logs to an issue.
- For a suspected vulnerability, contact the repository maintainer to establish a private reporting channel before sending details. Do not publish working credentials as proof.
- Windows requests administrator permission for this TUN client. There are no instructions to disable Defender, Smart App Control, TLS checks or other system protections.
- First run has no nodes, no preselected application names and no automatic connection. Startup is opt-in and preserves the connection switch state.
- Active IP checks send the node exit address to IPQuery. Download probes send traffic through the selected local SOCKS proxy to Cloudflare or CacheFly. These actions are manual, have time/data limits, and do not publish an online report.
- A provider risk score or an unmarked VPN/proxy flag is a database observation, not proof of residential status, anonymity or acceptance by any platform.

Follow docs/RELEASE-CHECKLIST.md before distributing a release. Credential scanning is an additional check, not a replacement for reviewing the exact commit and package contents.
