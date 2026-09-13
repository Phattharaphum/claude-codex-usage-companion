#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
EXTENSION_ID="claude-codex-usage-companion@ychsieh95.github.io"
SOURCE="$ROOT/packaging/gnome/$EXTENSION_ID"
TARGET_ROOT="${XDG_DATA_HOME:-$HOME/.local/share}/gnome-shell/extensions"
TARGET="$TARGET_ROOT/$EXTENSION_ID"

if ! command -v gnome-extensions >/dev/null 2>&1; then
  echo "gnome-extensions is required to install this GNOME Shell extension." >&2
  exit 1
fi

install -d -m 755 "$TARGET"
install -m 644 "$SOURCE/metadata.json" "$SOURCE/extension.js" \
  "$SOURCE/presentation.js" "$TARGET/"
gnome-extensions enable "$EXTENSION_ID"
echo "Enabled $EXTENSION_ID for the current user."
