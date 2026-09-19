#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
EXTENSION_ID="claude-codex-usage-companion-fork-v3@gamephat.local"
STALE_FORK_EXTENSION_IDS=(
  "claude-codex-usage-companion-fork-v2@gamephat.local"
  "claude-codex-usage-companion-fork@gamephat.local"
)
LEGACY_EXTENSION_ID="claude-codex-usage-companion@ychsieh95.github.io"
SOURCE="$ROOT/packaging/gnome/$LEGACY_EXTENSION_ID"
# A terminal launched by a sandboxed editor can override XDG_DATA_HOME with
# the editor's private data directory. GNOME Shell does not search that
# directory, so use its per-user extension location unless explicitly
# overridden for a nonstandard shell setup.
TARGET_ROOT="${GNOME_EXTENSION_DATA_HOME:-$HOME/.local/share}/gnome-shell/extensions"
TARGET="$TARGET_ROOT/$EXTENSION_ID"

if ! command -v gnome-extensions >/dev/null 2>&1; then
  echo "gnome-extensions is required to install this GNOME Shell extension." >&2
  exit 1
fi

gnome-extensions disable "$EXTENSION_ID" 2>/dev/null || true
install -d -m 755 "$TARGET"
install -m 644 "$SOURCE/metadata.json" "$SOURCE/extension.js" \
  "$SOURCE/presentation.js" "$SOURCE/stylesheet.css" "$TARGET/"
gnome-extensions disable "$LEGACY_EXTENSION_ID" 2>/dev/null || true
for stale_id in "${STALE_FORK_EXTENSION_IDS[@]}"; do
  gnome-extensions disable "$stale_id" 2>/dev/null || true
done
if gnome-extensions enable "$EXTENSION_ID" 2>/dev/null; then
  echo "Enabled $EXTENSION_ID for the current user."
else
  # GNOME Shell caches its extension registry for the life of a Wayland
  # session. Schedule a newly-versioned UUID for the next login when the
  # running shell cannot discover it yet.
  gjs -c '
    const {Gio} = imports.gi;
    const settings = new Gio.Settings({schema_id: "org.gnome.shell"});
    const requested = ARGV[0];
    const stale = new Set(ARGV.slice(1));
    const enabled = settings.get_strv("enabled-extensions")
        .filter(id => !stale.has(id));
    if (!enabled.includes(requested))
        enabled.push(requested);
    const disabled = settings.get_strv("disabled-extensions")
        .filter(id => id !== requested);
    for (const id of stale) {
        if (!disabled.includes(id))
            disabled.push(id);
    }
    settings.set_strv("enabled-extensions", enabled);
    settings.set_strv("disabled-extensions", disabled);
    Gio.Settings.sync();
  ' "$EXTENSION_ID" "$LEGACY_EXTENSION_ID" "${STALE_FORK_EXTENSION_IDS[@]}"
  echo "Installed $EXTENSION_ID. Log out and sign in once so GNOME Shell loads the new extension module."
fi
