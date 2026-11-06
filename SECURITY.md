# Security Policy

## Supported versions

| Version | Supported |
| ------- | --------- |
| 1.1.x   | ✅ |
| < 1.1   | ❌ |

## Reporting a vulnerability

**Do not open a public issue** for a security problem.

Report it privately via GitHub: go to the **Security** tab of this repository
→ **Report a vulnerability**. Describe the issue, the affected version, and
steps to reproduce if you can.

Your report will be acknowledged, investigated, and fixed in a new release.
Please allow reasonable time for a fix before disclosing publicly.

## Scope notes

This app intentionally performs privileged operations (uninstalling apps,
deleting leftover files and registry keys, managing startup entries and
services). Those actions always require user confirmation, and cleanup is
guarded by `PathGuard`. A report is most useful when it shows a way to bypass
those guards, or to delete or modify files outside what the user chose.
