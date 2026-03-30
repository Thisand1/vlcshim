# Security Policy

## Supported Versions

This project is small and moves quickly. Security fixes are only guaranteed for the newest code line.

| Version | Supported |
| ------- | --------- |
| `main` / unreleased development branch | Yes |
| Latest tagged release | Yes |
| Older releases | No |
| Random source snapshots / forks | No |

If a fix lands on `main`, older versions may not be backported.

## What To Report

Please report issues that could let someone:

- make the shim execute unintended code
- force the shim to talk to something other than the intended local VLC HTTP endpoint
- expose or misuse VLC HTTP credentials
- abuse the AIMP / Rainmeter compatibility layer to control the shim unexpectedly
- escape the intended local-only trust model

Good reports usually include:

- affected version, commit, or branch
- Windows version
- exact steps to reproduce
- whether VLC Web was enabled on `127.0.0.1` only
- proof of impact, not just a suspicious code pattern
- logs, screenshots, or a minimal proof-of-concept if safe to share

## Reporting A Vulnerability

Do not open a public GitHub issue for an unpatched security problem.

Preferred order:

1. Use GitHub Private Vulnerability Reporting for this repository if it is enabled.
2. If private reporting is not enabled, contact the maintainer privately through the contact method listed on the maintainer profile or repository metadata.
3. If neither option exists yet, open a normal issue only after the problem is fixed or after you have explicit permission to disclose it publicly.

When reporting, include:

- a short title
- affected version or commit
- impact summary
- reproduction steps
- any mitigations you already tested
- whether you want to be credited in a fix or advisory

## Response Expectations

Target handling, best effort:

- acknowledgment within 7 days
- initial triage within 14 days
- status updates when there is meaningful progress

This is an independent project, so these are goals, not a contractual SLA.

## Disclosure Guidance

Please give the maintainer reasonable time to validate and patch the issue before public disclosure.

Suggested default:

- 90 days for normal vulnerabilities
- shorter timelines are reasonable for issues that are already being exploited

If the report is out of scope or not reproducible, it may be closed with an explanation.

## Security Boundaries And Non-Goals

This shim is designed around a local-machine trust model. That matters.

Things this project tries to protect against:

- accidental exposure to non-loopback VLC HTTP targets
- command injection from logging helpers
- unsafe forwarding of local media-control commands

Things this project does not treat as a hard security boundary:

- cosmetic app identity spoofing for SMTC or shell integration
- local users on the same machine with equal privileges
- malicious software already running as the same user
- third-party Rainmeter skins, plugins, or other local tooling

If an attack already requires arbitrary local code execution as the same user, that is usually outside this project's security scope unless the shim makes the impact materially worse.

## Safe Deployment Recommendations

If you use this project, do the following:

- keep VLC Web bound to localhost only
- set a non-default Lua HTTP password
- do not expose VLC Web to your LAN or the internet
- do not run the shim elevated unless you truly need to
- only use builds from source you trust
- treat AIMP / Rainmeter compatibility mode as local-only integration, not a sandbox

## Current Hardening Notes

Recent hardening in this project includes:

- loopback-only VLC HTTP connections
- no PowerShell-based log tailing
- stricter handling of user-provided port values

Those measures reduce risk, but they do not make the shim safe to expose as a network service.

## Out-Of-Scope Reports

The following usually do not count as security vulnerabilities by themselves:

- the app showing the wrong player name or icon
- SMTC metadata being inaccurate
- a broken Rainmeter skin
- crashes without a security impact
- VLC Web using weak settings when the user explicitly configured it that way

If you are unsure, report it anyway and explain why you believe there is security impact.
