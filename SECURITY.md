# Security

## Reporting a vulnerability

Found a security issue? Please open a private report through GitHub's Security advisories
("Report a vulnerability" on the repo's Security tab) rather than a public issue. Include what
you found and how to reproduce it. I'll respond as soon as I can.

## What the plugin can access

- Reads and writes the game's own Party Finder recruitment state locally, so it can save and
  reapply your recruitment settings.
- Reads your party roster and the active listing to keep a listing refreshed.
- Stores your saved presets in the plugin's config folder on this PC.

Nothing the plugin touches leaves your machine. There is no network code, no telemetry, and no
third-party server. It only interacts with FINAL FANTASY XIV through Dalamud on the local
client.

## Secrets

No API keys, tokens, or client secrets are used or committed. The plugin holds no credentials.
