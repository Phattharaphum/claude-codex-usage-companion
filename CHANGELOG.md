# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Accessibility: the quota chart is keyboard-navigable (Tab to focus, ←/→ move in time, ↑/↓ switch provider, Home/End jump, Esc dismisses) and announces each sample; tabs, filter chips and range pills announce their position and selected state; quota cards, summary tiles and table rows are read as one sentence; icon-only buttons have names; scroll areas take keyboard focus.
- Automated guards: WCAG contrast tests over every window palette (4.5:1 for text, 3:1 for graphics) and a typography test that rejects text under 10 px.
- Usage insights: one window instead of four. Usage history, Analysis, Reset efficiency and the full Timeline are now the tabs Overview, Analysis, Efficiency and Records of a single window that shares one period and provider filter, so you no longer open and rearrange several windows. Open it from the "⋯" menu or with **I**; "Reset efficiency" in the menu jumps straight to its tab. Ctrl+1–4 switch tabs, Ctrl+Tab cycles them, and a coverage chip in the header shows how much of the period was recorded on every tab.
- Records: a search box (provider, error text or date), a "Changed only" switch that hides samples identical to the provider's previous one (it collapsed 16,660 rows to 1,623 on a real log, ignoring an idle window's drifting reset time), an "Errors only" switch, paging instead of a fixed 240-row cap, and Export CSV for the filtered rows.
- Settings: search now finds individual settings across every page (not just page names) and Ctrl+F focuses it; overlay opacity and screen margin are adjustable, previewed live on the overlay and restored if you cancel; a Notifications "Send test" button (with a warning when `notify-send` is missing); the Logging page shows the history file's size and record count and offers Open folder and a confirmed Clear history; About has links (project, what's new, issues, license, original project) and a Copy diagnostics summary that contains no credentials.
- Usage history now shows how much of a period was actually recorded. The app only samples while it runs, so the overview and analysis windows report data coverage and the number of gaps, the chart shades stretches with no samples as "No data" (and draws dotted connectors across them instead of a line that claims to know what happened), and hovering a gap shows its length.
- `usageLogRetentionDays` (default 90, `0` keeps everything) with a **History retention** choice in Settings → Logging. Older records are pruned in the background, at most every six hours, by streaming the file and replacing it atomically.

### Fixed

- Usage figures no longer inflate with the number of providers. "5-hour consumed 1,860%" is now "18.7×" (full quotas used) with the burn rate averaged per provider, so a combined rate is no longer compared with a per-provider "High" threshold. The projected runway is the first provider to run out judged on its own remaining quota, and "Lowest now" replaces a meaningless combined "start → latest".
- Reset efficiency no longer counts cycles it cannot judge. Resets seen after the app was not sampling, or of windows that were never used, are marked unverified and excluded from the averages (about half of the 30-day cycles in a real log); they can be shown dimmed with a switch. Utilization now reads "higher is better" like the overlay, an idle or unsampled cycle is no longer coloured red, and a 5-hour reset no longer displays the weekly quota as if it had reset too.
- The history log no longer fills with identical failure rows during an outage (one row when it starts or changes, a reminder every 30 minutes, and a new one after recovery).
- Usage history and Reset efficiency no longer freeze the window while the log loads. The log is read in the background with a loading bar, and parsed rows are cached so a refresh only reads the bytes appended since the last read (opening took ~360-650 ms on a 12 MB log).
- Antigravity no longer shows an old reading as if it were current when its local service stops answering. A snapshot kept after a failed refresh is now dimmed, labelled with the time it was read and marked "Stale" in the section summary, and a quota whose reset time has already passed says it is waiting for new data instead of counting down "0d 0h 0m".
- Antigravity failures now say what went wrong (not running, still starting, request rejected, timed out, unexpected data, or unreachable) instead of one generic "unavailable" message. `status --watch` also prints the error beside a retained snapshot.
- Antigravity refreshes no longer fail because one unresponsive port used up the whole time budget. The ports are probed concurrently, and the transport that answered last is tried first.
- Antigravity discovery reads listening ports from `/proc` and no longer needs `lsof` (kept only as a fallback).
- Claude usage no longer stalls at "Waiting for usage data" after a login autostart. The stored OAuth access token expires after a few hours and the Claude CLI renews it only while the CLI itself runs, so following a reboot the companion was reading an expired token and reporting "Claude session expired" until the user happened to open Claude Code. It now renews the token itself through the same OAuth refresh grant the CLI uses, and also retries once when the server rejects a token that has not reached its recorded expiry.

### Changed

- One vocabulary and one set of colours across the app. Providers are "Claude", "Codex", "Antigravity · Gemini" and "Antigravity · Claude & GPT" everywhere (the overlay, chart, tables and reports used to disagree: "Claude + ChatGPT", "C+GPT", "Claude and GPT models"), the quota windows are always "5-hour" and "Weekly" (not "5 HR", "5hr" and "Week"), and the 5-hour window is amber and the weekly window blue in the overlay, analysis, efficiency and summary tiles. Provider colours come from a single definition instead of five drifting copies. The green-to-red scale still only ever means "how much is left".
- Dates, weekdays and durations follow the interface language without relying on system locale data: "2026年10月11日 星期日", "10月11日 週日 16:02" and "6天 17小時" instead of English month names and "6d 17h" inside a Chinese window, including chart axes and tooltips.
- Known provider errors are translated when shown (for example "Antigravity 未執行。開啟後即可更新用量。"), in the overlay, the Records table and the history message; the log itself still stores the English text.
- Text and status colours now meet WCAG AA: the light-theme accent green was darkened, and the amber, red, neutral and info status colours used for words like "Moderate" or "High" were replaced with versions that stay readable on their own tinted pills in both themes. Labels that were 8.5-9.5 px are now 10 px.
- The keyboard-shortcut window matches the other windows (themed, custom title bar), draws each key as a keycap in two columns, lists the new Usage insights and Settings shortcuts, and also opens with **?** and from the insights window.
- Redesigned Settings. It now shares the overlay's neutral palette and green accent (toggles, sliders, focus rings and the selected page), every label is translated (several cards and hints were English-only), window positions read "Bottom right" instead of `right-bottom`, duplicated titles (page title repeated as the card title, "Update interval" twice) are gone, provider cards describe what they track instead of outdated model names ("Claude 3.5 / 3.7", "GPT-4o", "Gemini 2.5"), and date formats show real examples and a live preview ("Resets in 2d 3h · Oct 13 19:47") instead of raw tokens and a fixed August 6 sample. The log path now has a full-width field instead of being cut off.
- Redesigned the usage overlay. Quota cards now use one neutral surface for every provider (colour is reserved for the quota level), a clear "left" caption next to the percentage, a "Resets in 46m" countdown with the reset time beside it, and a bar that glides to new values. The coloured "5 HR" / "WEEK" chips became plain labels with a small cadence dot.
- A full window with no reset time now says "Starts on next use" instead of "Reset time unavailable".
- Antigravity groups are separated by a labelled divider instead of a grey pill, and its errors appear as a banner with a retry button beside the data they affect. Its error no longer repeats in the header, which now shows when it last updated successfully.
- The header is less crowded: Usage history moved into the "…" menu, and long status text wraps to a second line instead of being cut off.
- Refreshed the dark and light palettes (cooler neutrals, higher-contrast secondary text).
- The companion now writes to `~/.claude/.credentials.json` when it renews a token. The write is atomic, stays owner-readable only, preserves every field the app does not own, and takes the same lock file the Claude CLI uses so the two never rotate the refresh token at once. Tokens are still never logged.

## [0.1.2] - 2026-08-10

### Added

- A Plasma 6 widget with compact circle or bar views and an expanded popup showing reset times, credits, and provider-specific errors.
- Independent display toggles for Claude's current session and weekly limits and Codex's five-hour and weekly limits.
- A standalone `.plasmoid` release artifact and a source-checkout installation script for the Plasma widget.

### Changed

- The overlay now computes its height from the visible usage cards and remains anchored to the configured screen corner after resizing.
- Refreshed the README screenshots and removed the stale package-rename upgrade note.

### Migration

- Existing `showFiveHourLimit` settings migrate to `showCodexFiveHour`; the legacy key is removed the next time settings are saved.

## [0.1.1] - 2026-08-09

### Added

- Keyboard shortcuts list: a **?** button in the panel header and a **Keyboard shortcuts** item in the system-tray menu open a localized list of every shortcut, grouped by window. `F1` opens the same list, and `Esc`, `F1`, or **OK** closes it.
- A shutdown watchdog that exits the process if the shutdown sequence has not finished within 10 seconds, so a stalled step can no longer leave a resident process holding the single-instance lock.

### Changed

- Reordered the panel header controls to keyboard shortcuts, pin (always-on-top), settings, refresh, minimize, and close, with wider spacing separating the two window controls from the panel actions.
- Refreshed the README screenshots for all three languages.

### Fixed

- **Quit** (and closing the panel with the system tray disabled) hid the window but left the process running. Disposing the single-instance lease blocked the dispatcher thread waiting for its own accept loop, whose continuations were queued back to that same thread; the wedged dispatcher then prevented the shutdown sequence from ever calling `Shutdown()`. The accept loop now runs off the dispatcher and both disposal paths are bounded.

## [0.1.0] - 2026-08-01

### Added

- Initial Linux port of Claude Codex Usage Companion, based on [gkfriend/codex-usage-companion](https://github.com/gkfriend/codex-usage-companion) (originally Windows/WPF).
- Compact always-on-top desktop companion built with Avalonia.
- Scriptable CLI with one-shot, JSON, and live-watch output.
- Codex `SessionStart` and `Stop` hooks that start or refresh one resident GUI process.
- English, Traditional Chinese, and Simplified Chinese usage text.
- Settings window for language, theme, window position, usage alerts, reset notifications, usage logging, system tray, login autostart, and always-on-top behavior.
- `.deb` package with KDE/GNOME application-menu entry and icon.
- Claude Code usage support: an `enableClaudeUsage` setting (default on) adds a Claude section (current session and current week, all models) stacked below the existing Codex section in the overlay, tray tooltip, usage log, and CLI `status`/`watch` commands, with a colored dot marking each card's provider and a `Usage credits: used / limit` line for Claude's pay-as-you-go extra usage. Reads the OAuth access token Claude Code itself already maintains in `~/.claude/.credentials.json` (never written or refreshed by this app) and calls Anthropic's usage endpoint directly, polled no more often than every 180 seconds regardless of the configured refresh interval. `CLAUDE_CREDENTIALS_PATH` and `CLAUDE_CLI_PATH` environment variables mirror the existing `CODEX_CLI_PATH` override pattern.
- A matching `enableCodexUsage` setting (default on) independently shows or hides the Codex section, with a Settings checkbox alongside Claude's; the overlay height, CLI output, and usage log all adapt to whichever providers are enabled, including both being off.
- Main-window keyboard shortcuts: `S` opens Settings, `Ctrl+R` refreshes usage, and `Esc` closes the window.
- Settings keyboard shortcuts: `Ctrl+S` saves, while `Esc` closes the active settings or warning window.
- A localized confirmation warning when closing Settings with unsaved changes, with options to discard the changes or continue editing.
- Four configurable system-tray icon styles: the original icon, Claude current-session remaining, Claude weekly remaining, and Codex session remaining (with weekly fallback when the five-hour window is unavailable).
- Bold, anti-aliased seven-segment remaining-usage digits on a dark tray-icon background, without a percent sign.
- **Refresh usage** and **Start automatically when I sign in** actions in the system-tray context menu.
- A categorized Settings layout with separators and an **Apply** button that saves without closing the window.

### Changed

- Renamed the package, CLI commands, XDG paths, desktop entry, and autostart file from `codex-usage-companion` to `codex-claude-usage-companion`, and then again to `claude-codex-usage-companion`. Existing `settings.json` migrates automatically on first run from whichever older package's config is found; old `.deb`s should be removed manually (`sudo apt remove codex-usage-companion` and/or `sudo apt remove codex-claude-usage-companion`) since each rename used a different package name.
- **Breaking:** `status --json`/`watch --json` now nest results under `claude`/`codex` keys (in that order) instead of a flat object (each key is present only when its provider is enabled).
- **Breaking:** the usage-update log gains a `provider` column/field (`codex` or `claude`) in all three formats (txt/csv/jsonl).
- The Claude section (current session/week) now appears before the Codex section in the overlay, tray tooltip, and CLI output.
- Card titles show each provider's own icon (the official Claude mark and the OpenAI mark) instead of a `[Codex]`/`[Claude]` text prefix.
- Settings actions now appear in the standard **OK**, **Cancel**, **Apply** order and are localized in all supported languages.
- Updated the application, tests, CI, and release builds to .NET 10 and C# 14.

### Notes

- No telemetry. This app never stores or writes authentication tokens of its own; when Claude usage is enabled, it reads the OAuth access token Claude Code already stores locally and sends it only to Anthropic's official API over HTTPS.

[0.1.2]: https://github.com/ychsieh95/claude-codex-usage-companion/releases/tag/v0.1.2
[0.1.1]: https://github.com/ychsieh95/claude-codex-usage-companion/releases/tag/v0.1.1
[0.1.0]: https://github.com/ychsieh95/claude-codex-usage-companion/releases/tag/v0.1.0
