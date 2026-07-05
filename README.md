# Clarion

A Dalamud plugin that saves a full Party Finder recruitment setup as a preset and applies it in
one click. Duty, job slots, description, objective, loot rule, one player per job, language: all
of it, filled in from a saved preset instead of clicking through the form every time you re-form
a party.

It can also keep an active listing alive, re-confirming it near the end of its hour so it
doesn't quietly expire while you wait.

## What a preset stores

Everything the Recruitment Criteria form holds: the duty and its category, the objective, loot
rule, one-player-per-job, language flags, the per-slot job restrictions, and your description.
Save the form you have on screen, name it (or let it name itself after the duty), and it's
ready to reapply later.

## Applying a preset

- **Fill only** writes the preset into the recruitment form and reopens it so you can look it
  over before you post.
- **Fill + submit** does the same and posts the party in one action.

## Keeping a listing alive

Turn on auto-refresh and the plugin re-confirms your listing a few minutes before its hour runs
out, so it stays up without you re-posting. It works with the Party Finder windows closed and
leaves your away status alone, so if you're marked away with a frame limiter or notifier tied to
it, you stay that way.

## Installation

1. In-game, open **ESC → Dalamud Settings → Experimental → Custom Plugin Repositories** and add:
   ```
   https://aemiliusxiv.github.io/DalamudPlugins/pluginmaster.json
   ```
2. Open the Plugin Installer (`/xlplugins`) and search for **Clarion**.

Clarion is part of the [AemiliusXIV plugin repository](https://github.com/AemiliusXIV/DalamudPlugins); that page lists all available plugins.

## Usage

- `/clarion` opens the preset window. `/clarion cfg` opens settings.
- Open the in-game Recruitment Criteria form (Recruit Members) to save or apply, since that's
  where a listing is configured.

## Privacy

Nothing leaves your machine. The plugin reads and writes the game's own Party Finder state
locally and stores your presets in the plugin's config folder. There is no network code, no
telemetry, and no third-party server.

## License

Copyright (c) 2026 AemiliusXIV

This project is source-available. You may fork and modify it, but the source code may not be copied into other projects or plugins, in source or compiled form, without explicit written permission. Forks must preserve this license and credit the original author. See the [LICENSE](LICENSE) file for full terms.

This project is not affiliated with or endorsed by Square Enix Co., Ltd. The use of third-party tools is prohibited under the FINAL FANTASY XIV Terms of Service; use of this plugin is entirely at your own risk. FINAL FANTASY XIV is a registered trademark of Square Enix Holdings Co., Ltd.
