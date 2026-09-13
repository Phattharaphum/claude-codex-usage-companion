import Gio from 'gi://Gio';
import GLib from 'gi://GLib';
import St from 'gi://St';
import Clutter from 'gi://Clutter';
import Cairo from 'cairo';

import {Extension} from 'resource:///org/gnome/shell/extensions/extension.js';
import * as Main from 'resource:///org/gnome/shell/ui/main.js';
import * as PanelMenu from 'resource:///org/gnome/shell/ui/panelMenu.js';
import * as PopupMenu from 'resource:///org/gnome/shell/ui/popupMenu.js';

import {formatPanelText, formatPercent, normalizeState} from './presentation.js';

const APPLICATION_COMMAND = 'claude-codex-usage-companion';
const STATE_DIRECTORY = 'claude-codex-usage-companion';
const STATE_FILE = 'gnome-top-bar.json';

const METERS = [
    {name: 'Claude', glyph: '✳', color: [1.0, 0.31, 0.06], enabled: 'hasClaude', value: 'claudeFiveHourRemaining'},
    {name: 'Codex', glyph: '◌', color: [0.18, 0.85, 0.61], enabled: 'hasCodex', value: 'codexFiveHourRemaining'},
    {name: 'Antigravity', glyph: '✦', color: [0.93, 0.95, 0.08], enabled: 'hasAntigravity', value: 'antigravityRemaining'},
];

class RingMeter extends St.DrawingArea {
    _init(value, color) {
        super._init({style_class: 'ccuc-ring', reactive: false});
        this._value = value;
        this._color = color;
        this.set_size(46, 46);
    }

    vfunc_repaint() {
        const cr = this.get_context();
        const [width, height] = this.get_surface_size();
        const radius = Math.min(width, height) / 2 - 3;
        const centerX = width / 2;
        const centerY = height / 2;

        cr.setLineWidth(4);
        cr.setLineCap(Cairo.LineCap.ROUND);
        cr.setSourceRGBA(0.24, 0.25, 0.27, 1);
        cr.arc(centerX, centerY, radius, 0, Math.PI * 2);
        cr.stroke();

        if (this._value !== null) {
            cr.setSourceRGBA(...this._color, 1);
            cr.arc(centerX, centerY, radius, -Math.PI / 2,
                -Math.PI / 2 + (Math.PI * 2 * this._value / 100));
            cr.stroke();
        }

        cr.$dispose();
    }
}

export default class CompanionTopBarExtension extends Extension {
    enable() {
        this._state = null;
        this._stylesheet = this.dir.get_child('stylesheet.css');
        this.loadStylesheet(this._stylesheet);
        this._indicator = new PanelMenu.Button(0.0, this.metadata.name, false);
        this._label = new St.Label({text: formatPanelText(null)});
        this._indicator.add_child(this._label);
        Main.panel.addToStatusArea(this.uuid, this._indicator);

        const runtimeDirectory = GLib.get_user_runtime_dir();
        this._runtimeDirectory = Gio.File.new_for_path(runtimeDirectory);
        this._stateDirectory = this._runtimeDirectory.get_child(STATE_DIRECTORY);
        this._stateFile = this._stateDirectory.get_child(STATE_FILE);
        this._installRuntimeMonitor();
        this._installStateDirectoryMonitor();
        this._reloadState();
    }

    disable() {
        this._runtimeMonitor?.cancel();
        this._runtimeMonitor = null;
        this._stateDirectoryMonitor?.cancel();
        this._stateDirectoryMonitor = null;
        this._indicator?.destroy();
        this._indicator = null;
        this._label = null;
        this._state = null;
        this._runtimeDirectory = null;
        this._stateDirectory = null;
        this._stateFile = null;
        if (this._stylesheet) {
            this.unloadStylesheet(this._stylesheet);
            this._stylesheet = null;
        }
    }

    _installRuntimeMonitor() {
        try {
            this._runtimeMonitor = this._runtimeDirectory.monitor_directory(
                Gio.FileMonitorFlags.WATCH_MOVES,
                null);
            this._runtimeMonitor.connect('changed', () => {
                this._installStateDirectoryMonitor();
                this._reloadState();
            });
        } catch (error) {
            logError(error, `${this.metadata.uuid}: could not monitor XDG_RUNTIME_DIR`);
        }
    }

    _installStateDirectoryMonitor() {
        if (this._stateDirectoryMonitor || !this._stateDirectory.query_exists(null)) {
            return;
        }

        try {
            this._stateDirectoryMonitor = this._stateDirectory.monitor_directory(
                Gio.FileMonitorFlags.WATCH_MOVES,
                null);
            this._stateDirectoryMonitor.connect('changed', () => this._reloadState());
        } catch (error) {
            logError(error, `${this.metadata.uuid}: could not monitor companion state`);
        }
    }

    async _reloadState() {
        if (!this._stateFile || !this._indicator) {
            return;
        }

        try {
            const [, contents] = await this._stateFile.load_contents_async(null);
            this._state = normalizeState(JSON.parse(new TextDecoder().decode(contents)));
        } catch (_) {
            this._state = null;
        }

        this._updatePresentation();
    }

    _updatePresentation() {
        if (!this._indicator || !this._label) {
            return;
        }

        this._label.text = formatPanelText(this._state);
        this._indicator.menu.removeAll();
        this._addMeters();
        this._indicator.menu.addMenuItem(new PopupMenu.PopupSeparatorMenuItem());

        const openUsage = new PopupMenu.PopupMenuItem('Open Usage');
        openUsage.connect('activate', () => this._runCompanion('gui'));
        this._indicator.menu.addMenuItem(openUsage);
        const refresh = new PopupMenu.PopupMenuItem('Refresh');
        refresh.connect('activate', () => this._runCompanion('refresh'));
        this._indicator.menu.addMenuItem(refresh);
    }

    _addMeters() {
        const item = new PopupMenu.PopupBaseMenuItem({
            reactive: false,
            can_focus: false,
        });
        const meters = new St.BoxLayout({
            vertical: true,
            style_class: 'ccuc-meters',
            x_align: Clutter.ActorAlign.CENTER,
            x_expand: true,
        });

        for (const meter of METERS) {
            const enabled = this._state?.[meter.enabled] === true;
            const value = enabled ? this._state[meter.value] : null;
            const column = new St.BoxLayout({
                vertical: true,
                style_class: 'ccuc-meter',
                x_align: Clutter.ActorAlign.CENTER,
            });
            const overlay = new St.Widget({
                layout_manager: new Clutter.BinLayout(),
                x_align: Clutter.ActorAlign.CENTER,
            });
            overlay.add_child(new RingMeter(value, meter.color));
            overlay.add_child(new St.Label({
                text: meter.glyph,
                style_class: 'ccuc-meter-glyph',
                x_align: Clutter.ActorAlign.CENTER,
                y_align: Clutter.ActorAlign.CENTER,
            }));
            column.add_child(overlay);
            column.add_child(new St.Label({
                text: formatPercent(value),
                style_class: 'ccuc-meter-value',
                x_align: Clutter.ActorAlign.CENTER,
            }));
            column.add_child(new St.Label({
                text: meter.name,
                style_class: 'ccuc-meter-name',
                x_align: Clutter.ActorAlign.CENTER,
            }));
            meters.add_child(column);
        }

        item.add_child(meters);
        this._indicator.menu.addMenuItem(item);
    }

    _runCompanion(command) {
        try {
            GLib.spawn_command_line_async(`${APPLICATION_COMMAND} ${command}`);
        } catch (error) {
            logError(error, `${this.metadata.uuid}: could not signal companion`);
        }
    }
}
