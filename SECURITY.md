# Security Policy

## Reporting a Vulnerability

Please **do not** open a public GitHub issue for security vulnerabilities.

Report privately either way:

- GitHub's [Private Vulnerability Reporting](https://github.com/DotifyBIZ/pulsemap/security/advisories/new) (Security tab → Report a vulnerability) — opens a private discussion with maintainers before any details become public.
- Email **cert@dotify.biz**.

Include what you'd include in any good bug report: the affected version or commit, steps to reproduce, and the impact as you understand it.

## Supported Versions

Pulsemap has no stable release yet. Until the first release, report issues against the latest commit on `main`.

## What to expect

We'll acknowledge new reports and work with you on a fix before any public disclosure. Pulsemap is local-first with no server component — most report categories will concern the desktop application itself (file parsing, local data handling) rather than network-facing surfaces.

## Scope

This project is used on both Dotify's own infrastructure and client sites, but Pulsemap itself has no cloud or hosted backend — see the project README for what that does and doesn't cover.
