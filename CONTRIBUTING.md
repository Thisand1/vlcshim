# Contributing

Thanks for wanting to help.

This project is small, Windows-specific, and intentionally narrow in scope: it bridges VLC's Lua HTTP interface to Windows media integration features such as SMTC, plus compatibility work like Rainmeter / AIMP-facing behavior when that is enabled.

## Before You Open An Issue Or Pull Request

Please read these first:

- [README.md](./README.md)
- [README_BEFORE_CODE_OF_CONDUCT.md](./README_BEFORE_CODE_OF_CONDUCT.md)
- [CODE_OF_CONDUCT.md](./CODE_OF_CONDUCT.md)
- [SECURITY.md](./SECURITY.md)

If you are reporting a security problem, do not use a normal public issue unless the problem is already fixed or disclosure was approved.

## Good Contributions

Useful contributions include:

- bug fixes
- setup and documentation fixes
- Windows version compatibility fixes
- VLC HTTP integration improvements
- SMTC behavior fixes
- Rainmeter / AIMP compatibility fixes
- tray, toast, or log viewer usability improvements
- tests or verification steps that make regressions easier to catch

## Less Useful Contributions

These are likely to be rejected unless there is a strong reason:

- large refactors with no user-facing benefit
- style-only rewrites
- dependency additions for small problems
- changes that weaken the loopback-only security model
- changes that make the shim depend on remote services
- speculative rewrites of working code without a reproducible bug

## Development Setup

1. Install VLC.
2. Enable VLC `Web` in `Tools > Preferences > All > Main Interface > Main interfaces`.
3. Set a Lua HTTP password in `Main Interface > Lua`.
4. Restart VLC.
5. Install the `.NET 8 SDK` if you are building from source.
6. Build the shim with `dotnet build` or run `dnet-cbr.bat`.

Useful runtime options:

- `--password your_password`
- `--port 8080`
- `--ports 8080,4212`
- `VLC_HTTP_PASSWORD`

## Project Rules

When contributing, keep these constraints in mind:

- Keep VLC HTTP traffic local-only.
- Do not reintroduce proxy-based or non-loopback connection behavior.
- Do not add shell-based logging helpers when an in-process solution is possible.
- Keep changes small and reviewable.
- Prefer fixing the actual bug over adding another compatibility hack on top of it.
- Preserve existing user-facing behavior unless the change is intentional and documented.

## Code Style

This repository prefers:

- clear, direct code
- practical fixes over framework-heavy abstractions
- minimal dependencies
- readable docs instead of placeholder text
- comments only where they actually help

If you touch old code, it is fine to clean it up a bit, but do not turn a bug fix into a repo-wide rewrite.

## Pull Request Guidance

A good pull request should:

- explain the problem
- explain the fix
- keep unrelated edits out
- mention the Windows build and VLC setup used for testing
- include screenshots or log examples if the change affects UI, tray behavior, logging, or notifications
- update docs if behavior, flags, or setup changed

If your change affects:

- SMTC behavior: mention what appeared in the Windows media flyout
- Rainmeter / AIMP compatibility: mention what skin, player measure, or external tool you tested with
- security-sensitive code: explain the threat model impact clearly

## Issue Reports

When reporting a bug, include:

- what you expected
- what actually happened
- exact reproduction steps
- Windows version
- VLC version if relevant
- whether VLC Web is enabled
- the port and password setup if relevant
- relevant log lines

“It broke” is not actionable.

## Documentation Contributions

Documentation fixes are welcome, especially if:

- setup instructions were unclear
- a warning was missing
- a feature exists but is not documented
- a confusing term can be rewritten more plainly

You do not need to wait for permission to fix obvious doc problems.

## Review And Merge Expectations

This is not a large project with guaranteed turnaround times.

Pull requests may be:

- merged
- asked to change
- left open until they are easier to test
- closed if they are out of scope

If a PR is rejected, it does not automatically mean the idea was bad. It may just not fit the current direction of the project.

## If You Fork

Forks are allowed.

Please:

- keep attribution
- be honest about what your fork changes
- do not present your fork as upstream

## Final Note

If you want to help, the highest-value contributions are usually:

- fixing reproducible bugs
- improving docs for first-time users
- tightening Windows / VLC compatibility
- reducing fragile behavior without making the project heavier
