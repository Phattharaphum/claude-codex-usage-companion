#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIRECTORY="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
SKIP_BUILD=false
for argument in "$@"; do
  case "$argument" in
    --no-build) SKIP_BUILD=true ;;
    --help|-h)
      echo "Usage: bash scripts/install-desktop.sh [--no-build]"
      echo "Installs the app, CLI aliases, logo, and a desktop shortcut for the current user."
      echo "No sudo is needed. --no-build uses an existing artifacts/publish build."
      exit 0 ;;
    *) echo "Unknown option: $argument" >&2; exit 2 ;;
  esac
done

case "$(uname -m)" in
  x86_64) RUNTIME_ID=linux-x64 ;;
  aarch64|arm64) RUNTIME_ID=linux-arm64 ;;
  *) echo "This installer supports x64 and ARM64 Linux." >&2; exit 1 ;;
esac

if [[ -f "$SCRIPT_DIRECTORY/claude-codex-usage-companion" ]]; then
  # The portable archive includes this script beside its executable.
  SOURCE_EXECUTABLE="$SCRIPT_DIRECTORY/claude-codex-usage-companion"
  DESKTOP_TEMPLATE="$SCRIPT_DIRECTORY/claude-codex-usage-companion.desktop"
  SOURCE_ICON="$SCRIPT_DIRECTORY/claude-codex-usage-companion.svg"
  SOURCE_PNG="$SCRIPT_DIRECTORY/claude-codex-usage-companion.png"
else
  PROJECT_ROOT="$(cd -- "$SCRIPT_DIRECTORY/.." && pwd)"
  PUBLISH_DIRECTORY="$PROJECT_ROOT/artifacts/publish/$RUNTIME_ID"
  SOURCE_EXECUTABLE="$PUBLISH_DIRECTORY/CodexUsageCompanion"
  DESKTOP_TEMPLATE="$PROJECT_ROOT/packaging/linux/claude-codex-usage-companion.desktop"
  SOURCE_ICON="$PROJECT_ROOT/packaging/linux/claude-codex-usage-companion.svg"
  SOURCE_PNG="$PROJECT_ROOT/src/CodexUsageCompanion/Assets/claude-codex-usage-companion.png"
  if [[ "$SKIP_BUILD" == false ]]; then
    dotnet publish "$PROJECT_ROOT/src/CodexUsageCompanion/CodexUsageCompanion.csproj" \
      --configuration Release --runtime "$RUNTIME_ID" --self-contained true \
      -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
      -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false \
      -p:TreatWarningsAsErrors=true -p:UseSharedCompilation=false \
      -p:DebugType=None -p:DebugSymbols=false --output "$PUBLISH_DIRECTORY"
  fi
fi

if [[ ! -x "$SOURCE_EXECUTABLE" ]]; then
  echo "No published executable found. Run this installer without --no-build first." >&2
  exit 1
fi

DATA_DIRECTORY="${XDG_DATA_HOME:-$HOME/.local/share}"
INSTALL_DIRECTORY="$DATA_DIRECTORY/claude-codex-usage-companion"
BIN_DIRECTORY="${COMPANION_BIN_DIR:-$HOME/.local/bin}"
APPLICATIONS_DIRECTORY="$DATA_DIRECTORY/applications"
APPLICATION_ID=claude-codex-usage-companion
INSTALLED_EXECUTABLE="$INSTALL_DIRECTORY/CodexUsageCompanion"
DESKTOP_ENTRY="$APPLICATIONS_DIRECTORY/$APPLICATION_ID.desktop"

if [[ "$DATA_DIRECTORY" != /* || "$DATA_DIRECTORY" == *$'\n'* || "$DATA_DIRECTORY" == *$'\r'* ]]; then
  echo "XDG_DATA_HOME must be an absolute path without line breaks." >&2
  exit 1
fi

install -d -m 755 "$INSTALL_DIRECTORY" "$BIN_DIRECTORY" "$APPLICATIONS_DIRECTORY" \
  "$DATA_DIRECTORY/icons/hicolor/scalable/apps" "$DATA_DIRECTORY/icons/hicolor/256x256/apps"
# Rename into place so updating an executable that is currently running works.
TEMP_EXECUTABLE="$(mktemp "$INSTALL_DIRECTORY/.companion.XXXXXX")"
trap 'rm -f -- "$TEMP_EXECUTABLE"' EXIT
install -m 755 "$SOURCE_EXECUTABLE" "$TEMP_EXECUTABLE"
mv -f -- "$TEMP_EXECUTABLE" "$INSTALLED_EXECUTABLE"
ln -sfn "$INSTALLED_EXECUTABLE" "$BIN_DIRECTORY/$APPLICATION_ID"
ln -sfn "$INSTALLED_EXECUTABLE" "$BIN_DIRECTORY/claude-codex-usage"
install -m 644 "$SOURCE_ICON" "$DATA_DIRECTORY/icons/hicolor/scalable/apps/$APPLICATION_ID.svg"
install -m 644 "$SOURCE_PNG" "$DATA_DIRECTORY/icons/hicolor/256x256/apps/$APPLICATION_ID.png"

quote_exec_argument() {
  local escaped="$1"
  escaped="${escaped//\\/\\\\}"
  escaped="${escaped//\"/\\\"}"
  escaped="${escaped//\$/\\\$}"
  escaped="${escaped//\`/\\\`}"
  escaped="${escaped//%/%%}"
  # Desktop string escaping is decoded before Exec argument quoting.
  escaped="${escaped//\\/\\\\}"
  printf '"%s"' "$escaped"
}

while IFS= read -r line || [[ -n "$line" ]]; do
  case "$line" in
    Exec=*)
      printf 'Exec=/usr/bin/env -u SESSION_MANAGER AVALONIA_X11_USE_SESSION_MANAGEMENT=0 %s gui\n' \
        "$(quote_exec_argument "$INSTALLED_EXECUTABLE")" ;;
    *) printf '%s\n' "$line" ;;
  esac
done < "$DESKTOP_TEMPLATE" > "$DESKTOP_ENTRY"
chmod 644 "$DESKTOP_ENTRY"

DESKTOP_DIRECTORY="${COMPANION_DESKTOP_DIR:-}"
if [[ -z "$DESKTOP_DIRECTORY" ]]; then
  if command -v xdg-user-dir >/dev/null 2>&1; then
    DESKTOP_DIRECTORY="$(xdg-user-dir DESKTOP)"
  else
    DESKTOP_DIRECTORY="$HOME/Desktop"
  fi
fi
if [[ -n "$DESKTOP_DIRECTORY" && "$DESKTOP_DIRECTORY" != "$HOME" ]]; then
  install -d -m 755 "$DESKTOP_DIRECTORY"
  SHORTCUT="$DESKTOP_DIRECTORY/$APPLICATION_ID.desktop"
  install -m 755 "$DESKTOP_ENTRY" "$SHORTCUT"
  if command -v gio >/dev/null 2>&1; then
    # Sandboxed editors can replace GIO's module directory and hide GVFS,
    # preventing GNOME from marking this shortcut as trusted for double-click.
    env -u GIO_MODULE_DIR -u GIO_EXTRA_MODULES \
      gio set -t string "$SHORTCUT" metadata::trusted true 2>/dev/null || true
  fi
  echo "Desktop shortcut: $SHORTCUT"
fi

if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "$APPLICATIONS_DIRECTORY"
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
  gtk-update-icon-cache --force --ignore-theme-index "$DATA_DIRECTORY/icons/hicolor" >/dev/null 2>&1 || true
fi
echo "Installed: $INSTALLED_EXECUTABLE"
echo "Open Claude Codex Usage Companion from the application menu or double-click its desktop shortcut."
