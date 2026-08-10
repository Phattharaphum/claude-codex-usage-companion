#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WIDGET_ID="com.github.ychsieh95.claude-codex-usage-companion"
PACKAGE="$ROOT/packaging/plasma/$WIDGET_ID"

if ! command -v kpackagetool6 >/dev/null 2>&1; then
  echo "kpackagetool6 was not found. Install Plasma 6 before installing the widget." >&2
  exit 1
fi

if kpackagetool6 --type Plasma/Applet --show "$WIDGET_ID" >/dev/null 2>&1; then
  kpackagetool6 --type Plasma/Applet --upgrade "$PACKAGE"
else
  kpackagetool6 --type Plasma/Applet --install "$PACKAGE"
fi

echo "Installed Claude Codex Usage. Add it from Plasma's Add Widgets panel."
echo "If the widget was already running, restart Plasma to load the updated QML:"
echo "  systemctl --user restart plasma-plasmashell.service"
