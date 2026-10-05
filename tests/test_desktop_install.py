"""Behavior checks for the portable installer using isolated per-test paths."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import time
import unittest


ROOT = Path(__file__).resolve().parents[1]
APPLICATION_ID = "claude-codex-usage-companion"
GJS_LAUNCH = """
const {GioUnix} = imports.gi;
const app = GioUnix.DesktopAppInfo.new_from_filename(ARGV[0]);
if (!app || !app.launch([], null))
    throw new Error('Desktop launch failed');
"""


class DesktopInstallTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="companion-install-test-")
        self.addCleanup(self.temporary.cleanup)
        self.directory = Path(self.temporary.name)
        self.portable = self.directory / "portable archive"
        self.portable.mkdir()
        shutil.copyfile(ROOT / "scripts/install-desktop.sh", self.portable / "install-desktop.sh")
        for suffix in ["desktop", "svg"]:
            shutil.copyfile(ROOT / f"packaging/linux/{APPLICATION_ID}.{suffix}", self.portable / f"{APPLICATION_ID}.{suffix}")
        shutil.copyfile(ROOT / f"src/CodexUsageCompanion/Assets/{APPLICATION_ID}.png", self.portable / f"{APPLICATION_ID}.png")
        executable = self.portable / APPLICATION_ID
        executable.write_text("#!/usr/bin/python3\nimport json, os, sys\n"
                              "with open(os.environ['COMPANION_TEST_OUTPUT'], 'w') as output:\n"
                              "    json.dump({'argv': sys.argv, 'sessionManager': os.environ.get('SESSION_MANAGER'), "
                              "'avalonia': os.environ.get('AVALONIA_X11_USE_SESSION_MANAGEMENT')}, output)\n")
        executable.chmod(0o755)
        self.data = self.directory / "data"
        self.bin = self.directory / "bin"
        self.desktop = self.directory / "desktop"
        self.env = dict(os.environ, XDG_DATA_HOME=str(self.data), COMPANION_BIN_DIR=str(self.bin), COMPANION_DESKTOP_DIR=str(self.desktop))

    def install(self, check=True):
        return subprocess.run(["bash", str(self.portable / "install-desktop.sh"), "--no-build"],
                              env=self.env, text=True, capture_output=True, timeout=15, check=check)

    def launch(self, desktop_file, expected_executable):
        output = self.directory / "argv.json"
        output.unlink(missing_ok=True)
        env = dict(self.env, COMPANION_TEST_OUTPUT=str(output), SESSION_MANAGER="test-session-must-be-removed")
        subprocess.run(["gjs", "-c", GJS_LAUNCH, str(desktop_file)], env=env, text=True, capture_output=True, timeout=5, check=True)
        deadline = time.monotonic() + 5
        while not output.exists() and time.monotonic() < deadline:
            time.sleep(0.02)
        result = json.loads(output.read_text())
        self.assertEqual(result["argv"], [str(expected_executable), "gui"])
        self.assertIsNone(result["sessionManager"])
        self.assertEqual(result["avalonia"], "0")

    @unittest.skipUnless(shutil.which("gjs"), "GJS desktop parser is required for the integration check")
    def test_portable_install_and_desktop_launch_preserve_special_path_characters(self):
        self.data = self.directory / 'space "quote" dollar$HOME back`tick 100% literal\\slash'
        self.env["XDG_DATA_HOME"] = str(self.data)
        self.install()
        installed = self.data / APPLICATION_ID / "CodexUsageCompanion"
        menu = self.data / "applications" / f"{APPLICATION_ID}.desktop"
        shortcut = self.desktop / f"{APPLICATION_ID}.desktop"
        self.assertTrue(installed.stat().st_mode & 0o111)
        self.assertEqual((self.bin / APPLICATION_ID).resolve(), installed)
        self.assertEqual((self.bin / "claude-codex-usage").resolve(), installed)
        self.assertTrue(shortcut.stat().st_mode & 0o111)
        self.assertEqual(menu.read_bytes(), shortcut.read_bytes())
        self.assertTrue((self.data / f"icons/hicolor/scalable/apps/{APPLICATION_ID}.svg").is_file())
        self.assertTrue((self.data / f"icons/hicolor/256x256/apps/{APPLICATION_ID}.png").is_file())
        subprocess.run(["desktop-file-validate", str(menu)], capture_output=True, text=True, check=True)
        self.launch(menu, installed)
        self.launch(shortcut, installed)
        self.install()  # Reinstalling must retain valid aliases and shortcuts.
        self.launch(menu, installed)

    def test_invalid_data_paths_fail_before_installing(self):
        for path in ["relative-directory", str(self.directory / "line\nbreak"), str(self.directory / "line\rbreak")]:
            with self.subTest(path=path):
                self.env["XDG_DATA_HOME"] = path
                result = self.install(check=False)
                self.assertNotEqual(result.returncode, 0)
                self.assertFalse(self.bin.exists())
                self.assertFalse(self.desktop.exists())


if __name__ == "__main__":
    unittest.main()
