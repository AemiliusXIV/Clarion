# Clarion

A Dalamud plugin that saves your full Party Finder recruitment setup as a named preset and
reapplies it in one click, instead of clicking through the whole form every time you re-form a
party.

It can also keep an active listing alive, re-confirming it near the end of its hour so it
doesn't quietly expire while you wait.

## What a preset stores

Everything the Recruitment Criteria form holds: the duty and its category, the objective, loot
rule, one-player-per-job, language flags, the per-slot job restrictions, and your description.
Save the form you have on screen, name it (or let it name itself after the duty), and it's
ready to reapply later.

## Applying a preset

- **Fill only** loads the preset into the recruitment form and reopens it so you can look it over
  before you post.
- **Fill & recruit** loads it and posts the party in one action.

You don't need anything open first. If Party Finder is closed, applying opens it and the
recruitment form for you, so **Fill & recruit** can go from nothing to a posted party in a single
click.

## Quick pop-up

Whenever the in-game recruitment form is open, a small pop-up appears beside it: your last-used
preset on a button, a dropdown for the rest, and a link into the full window. It follows the form
on screen so you can apply without opening the main window. Move it, or turn it off, in settings.

## Keeping a listing alive

Turn on auto-refresh and Clarion re-confirms your listing before its hour runs out, so it stays up
without you re-posting. It picks up any live listing, whether you posted it through Clarion or by
hand, works with the Party Finder windows closed, and leaves your away status alone (so a frame
limiter or notifier tied to it keeps running). You set how many minutes should be left when it
refreshes.

## Installation

1. In-game, open **ESC → Dalamud Settings → Experimental → Custom Plugin Repositories** and add:
   ```
   https://aemiliusxiv.github.io/DalamudPlugins/pluginmaster.json
   ```
2. Open the Plugin Installer (`/xlplugins`) and search for **Clarion**.

Clarion is part of the [AemiliusXIV plugin repository](https://github.com/AemiliusXIV/DalamudPlugins); that page lists all available plugins.

## Usage

- `/clarion` opens the preset window. `/clarion cfg` opens settings.
- To **save** a preset, open the in-game Recruitment Criteria form (Recruit Members) first, since a
  preset is a snapshot of the form on screen. To **apply** one you don't need anything open;
  Clarion opens Party Finder and the form for you.
- Hover the (?) beside a preset to see exactly what it would set.

## Privacy

Nothing leaves your machine. The plugin reads and writes the game's own Party Finder state
locally and stores your presets in the plugin's config folder. There is no network code, no
telemetry, and no third-party server.

## License

Copyright (c) 2026 AemiliusXIV

This project is source-available. You may fork and modify it, but the source code may not be copied into other projects or plugins, in source or compiled form, without explicit written permission. Forks must preserve this license and credit the original author. See the [LICENSE](LICENSE) file for full terms.

This project is not affiliated with or endorsed by Square Enix Co., Ltd. The use of third-party tools is prohibited under the FINAL FANTASY XIV Terms of Service; use of this plugin is entirely at your own risk. FINAL FANTASY XIV is a registered trademark of Square Enix Holdings Co., Ltd.
