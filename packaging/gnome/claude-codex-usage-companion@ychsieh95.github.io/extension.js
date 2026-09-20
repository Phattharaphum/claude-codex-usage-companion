import Gio from 'gi://Gio';
import GLib from 'gi://GLib';
import GObject from 'gi://GObject';
import St from 'gi://St';
import Clutter from 'gi://Clutter';
import Cairo from 'cairo';

import {Extension} from 'resource:///org/gnome/shell/extensions/extension.js';
import * as Main from 'resource:///org/gnome/shell/ui/main.js';
import * as PanelMenu from 'resource:///org/gnome/shell/ui/panelMenu.js';
import * as PopupMenu from 'resource:///org/gnome/shell/ui/popupMenu.js';

import {
    connectionState,
    formatPanelText,
    formatPercent,
    formatResetTime,
    formatUpdatedTime,
    normalizeState,
} from './presentation.js';

Gio._promisify(Gio.File.prototype, 'load_contents_async', 'load_contents_finish');

const APPLICATION_COMMAND = 'claude-codex-usage-companion';
const STATE_DIRECTORY = 'claude-codex-usage-companion';
const STATE_FILE = 'gnome-top-bar.json';

const METERS = [
    {
        id: 'claude',
        name: 'Claude',
        iconFile: 'claude-symbolic.svg',
        fallbackGlyph: '✳',
        color: [1.0, 0.31, 0.09],
        enabled: 'hasClaude',
        value: 'claudeFiveHourRemaining',
        weekly: 'claudeWeeklyRemaining',
        reset: 'claudeFiveHourResetUnixMilliseconds',
        weeklyReset: 'claudeWeeklyResetUnixMilliseconds',
    },
    {
        id: 'codex',
        name: 'Codex',
        iconFile: 'openai-symbolic.svg',
        fallbackGlyph: '◌',
        color: [0.12, 0.89, 0.61],
        enabled: 'hasCodex',
        value: 'codexFiveHourRemaining',
        weekly: 'codexWeeklyRemaining',
        reset: 'codexFiveHourResetUnixMilliseconds',
        weeklyReset: 'codexWeeklyResetUnixMilliseconds',
    },
    {
        id: 'antigravity-gemini',
        name: 'Antigravity · Gemini',
        iconCandidates: [
            '/snap/antigravity/current/share/icons/hicolor/256x256/apps/antigravity.png',
            '/opt/antigravity-ide/resources/app/resources/linux/code.png',
        ],
        fallbackGlyph: '∩',
        color: [0.26, 0.58, 1.0],
        enabled: 'hasAntigravity',
        value: 'geminiFiveHourRemaining',
        weekly: 'geminiWeeklyRemaining',
        reset: 'geminiFiveHourResetUnixMilliseconds',
        weeklyReset: 'geminiWeeklyResetUnixMilliseconds',
    },
    {
        id: 'antigravity-claude-gpt',
        name: 'Antigravity · Claude + GPT',
        iconCandidates: [
            '/snap/antigravity/current/share/icons/hicolor/256x256/apps/antigravity.png',
            '/opt/antigravity-ide/resources/app/resources/linux/code.png',
        ],
        fallbackGlyph: '∩',
        color: [0.75, 0.38, 0.96],
        enabled: 'hasAntigravity',
        value: 'claudeGptFiveHourRemaining',
        weekly: 'claudeGptWeeklyRemaining',
        reset: 'claudeGptFiveHourResetUnixMilliseconds',
        weeklyReset: 'claudeGptWeeklyResetUnixMilliseconds',
    },
];

const RingMeter = GObject.registerClass(class RingMeter extends St.DrawingArea {
    _init(value, color, size = 46, strokeWidth = 4) {
        super._init({style_class: 'ccuc-ring', reactive: false});
        this._value = value;
        this._color = color;
        this._strokeWidth = strokeWidth;
        this.set_size(size, size);
    }

    vfunc_repaint() {
        const cr = this.get_context();
        const [width, height] = this.get_surface_size();
        const radius = Math.max(0, Math.min(width, height) / 2 - this._strokeWidth / 2);
        const centerX = width / 2;
        const centerY = height / 2;

        cr.setLineWidth(this._strokeWidth);
        cr.setLineCap(Cairo.LineCap.ROUND);
        cr.setSourceRGBA(0.25, 0.27, 0.30, 1);
        cr.arc(centerX, centerY, radius, 0, Math.PI * 2);
        cr.stroke();

        if (this._value !== null && this._value > 0) {
            cr.setSourceRGBA(...this._color, 1);
            cr.arc(centerX, centerY, radius, -Math.PI / 2,
                -Math.PI / 2 + Math.PI * 2 * this._value / 100);
            cr.stroke();
        }

        cr.$dispose();
    }
});

const BarMeter = GObject.registerClass(class BarMeter extends St.DrawingArea {
    _init(value, color) {
        super._init({
            style_class: 'ccuc-progress',
            reactive: false,
            x_expand: true,
            y_align: Clutter.ActorAlign.CENTER,
        });
        this._value = value;
        this._color = color;
        this.set_height(6);
    }

    vfunc_repaint() {
        const cr = this.get_context();
        const [width, height] = this.get_surface_size();
        const start = height / 2;
        const end = Math.max(start, width - height / 2);

        cr.setLineWidth(height);
        cr.setLineCap(Cairo.LineCap.ROUND);
        cr.setSourceRGBA(0.20, 0.22, 0.25, 1);
        cr.moveTo(start, height / 2);
        cr.lineTo(end, height / 2);
        cr.stroke();

        if (this._value !== null && this._value > 0) {
            cr.setSourceRGBA(...this._color, 1);
            cr.moveTo(start, height / 2);
            cr.lineTo(start + (end - start) * this._value / 100, height / 2);
            cr.stroke();
        }

        cr.$dispose();
    }
});

export default class CompanionTopBarExtension extends Extension {
    enable() {
        this._state = null;
        this._reloadSourceId = 0;
        this._stylesheet = this.dir.get_child('stylesheet.css');
        this._stylesheetLoaded = typeof this.loadStylesheet === 'function';
        if (this._stylesheetLoaded)
            this.loadStylesheet(this._stylesheet);

        this._indicator = new PanelMenu.Button(0.0, this.metadata.name, false);
        this._panelBox = new St.BoxLayout({
            style_class: 'ccuc-panel',
            y_align: Clutter.ActorAlign.CENTER,
        });
        this._indicator.add_child(this._panelBox);
        this._indicator.menu.connect('open-state-changed', (_menu, isOpen) => {
            if (isOpen)
                this._scheduleReload(0);
        });
        Main.panel.addToStatusArea(this.uuid, this._indicator);

        const runtimeDirectory = GLib.get_user_runtime_dir();
        this._runtimeDirectory = Gio.File.new_for_path(runtimeDirectory);
        this._stateDirectory = this._runtimeDirectory.get_child(STATE_DIRECTORY);
        this._stateFile = this._stateDirectory.get_child(STATE_FILE);
        this._installRuntimeMonitor();
        this._installStateDirectoryMonitor();
        this._updatePresentation();
        this._scheduleReload(0);
    }

    disable() {
        if (this._reloadSourceId) {
            GLib.source_remove(this._reloadSourceId);
            this._reloadSourceId = 0;
        }
        this._runtimeMonitor?.cancel();
        this._runtimeMonitor = null;
        this._stateDirectoryMonitor?.cancel();
        this._stateDirectoryMonitor = null;
        this._indicator?.destroy();
        this._indicator = null;
        this._panelBox = null;
        this._state = null;
        this._runtimeDirectory = null;
        this._stateDirectory = null;
        this._stateFile = null;
        if (this._stylesheet && this._stylesheetLoaded &&
            typeof this.unloadStylesheet === 'function') {
            this.unloadStylesheet(this._stylesheet);
        }
        this._stylesheetLoaded = false;
        this._stylesheet = null;
    }

    _installRuntimeMonitor() {
        try {
            this._runtimeMonitor = this._runtimeDirectory.monitor_directory(
                Gio.FileMonitorFlags.WATCH_MOVES,
                null);
            this._runtimeMonitor.connect('changed', () => {
                this._installStateDirectoryMonitor();
                this._scheduleReload();
            });
        } catch (error) {
            logError(error, `${this.metadata.uuid}: could not monitor XDG_RUNTIME_DIR`);
        }
    }

    _installStateDirectoryMonitor() {
        if (this._stateDirectoryMonitor || !this._stateDirectory.query_exists(null))
            return;

        try {
            this._stateDirectoryMonitor = this._stateDirectory.monitor_directory(
                Gio.FileMonitorFlags.WATCH_MOVES,
                null);
            this._stateDirectoryMonitor.connect('changed', () => this._scheduleReload());
        } catch (error) {
            logError(error, `${this.metadata.uuid}: could not monitor companion state`);
        }
    }

    _scheduleReload(delay = 80) {
        if (this._reloadSourceId)
            GLib.source_remove(this._reloadSourceId);

        this._reloadSourceId = GLib.timeout_add(
            GLib.PRIORITY_DEFAULT,
            delay,
            () => {
                this._reloadSourceId = 0;
                this._reloadState().catch(error =>
                    logError(error, `${this.metadata.uuid}: could not update presentation`));
                return GLib.SOURCE_REMOVE;
            });
    }

    async _reloadState() {
        if (!this._stateFile || !this._indicator)
            return;

        try {
            const [contents] = await this._stateFile.load_contents_async(null);
            this._state = normalizeState(JSON.parse(new TextDecoder().decode(contents)));
        } catch (_) {
            // Atomic replacement can briefly make a read fail. Only switch to
            // offline when the producer's state file is actually absent.
            if (!this._stateFile.query_exists(null))
                this._state = null;
        }

        this._updatePresentation();
    }

    _updatePresentation() {
        if (!this._indicator || !this._panelBox)
            return;

        this._renderPanel();
        this._indicator.menu.removeAll();
        this._addDashboard();
        this._indicator.menu.addMenuItem(new PopupMenu.PopupSeparatorMenuItem());

        const openUsage = new PopupMenu.PopupMenuItem(
            this._state ? 'Open Usage Companion' : 'Start Usage Companion');
        openUsage.connect('activate', () => this._runCompanion('gui'));
        this._indicator.menu.addMenuItem(openUsage);

        const refresh = new PopupMenu.PopupMenuItem('Refresh now');
        refresh.connect('activate', () => this._runCompanion('refresh'));
        this._indicator.menu.addMenuItem(refresh);
    }

    _renderPanel() {
        for (const child of this._panelBox.get_children())
            child.destroy();

        const status = connectionState(this._state);
        this._indicator.accessible_name = formatPanelText(this._state);
        if (status !== 'live') {
            const icon = new St.Icon({
                icon_name: status === 'offline'
                    ? 'process-stop-symbolic'
                    : 'emblem-synchronizing-symbolic',
                style_class: 'ccuc-panel-status-icon',
            });
            this._panelBox.add_child(icon);
            this._panelBox.add_child(new St.Label({
                text: status === 'offline' ? 'Offline' : 'Syncing',
                style_class: `ccuc-panel-status ccuc-panel-status-${status}`,
                y_align: Clutter.ActorAlign.CENTER,
            }));
            return;
        }

        for (const meter of METERS) {
            if (this._state?.[meter.enabled] !== true)
                continue;

            const value = this._state[meter.value] ?? this._state[meter.weekly];
            if (value === null)
                continue;

            const item = new St.BoxLayout({
                style_class: `ccuc-panel-meter ccuc-panel-meter-${meter.id}`,
                y_align: Clutter.ActorAlign.CENTER,
            });
            const overlay = new St.Widget({
                layout_manager: new Clutter.BinLayout(),
                width: 20,
                height: 20,
                y_align: Clutter.ActorAlign.CENTER,
            });
            overlay.add_child(new RingMeter(value, meter.color, 20, 2.5));
            overlay.add_child(this._createProviderIcon(
                meter,
                'ccuc-panel-provider-icon',
                'ccuc-panel-glyph'));
            item.add_child(overlay);
            item.add_child(new St.Label({
                text: formatPercent(value),
                style_class: 'ccuc-panel-value',
                y_align: Clutter.ActorAlign.CENTER,
            }));
            this._panelBox.add_child(item);
        }

    }

    _addDashboard() {
        const item = new PopupMenu.PopupBaseMenuItem({
            reactive: false,
            can_focus: false,
        });
        const dashboard = new St.BoxLayout({
            vertical: true,
            style_class: 'ccuc-dashboard',
            x_expand: true,
        });
        dashboard.add_child(this._createHeader());

        const status = connectionState(this._state);
        if (status === 'offline') {
            dashboard.add_child(this._createEmptyState(
                'process-stop-symbolic',
                'Companion is offline',
                'Start the app to show live quota data.'));
        } else if (status === 'syncing') {
            dashboard.add_child(this._createEmptyState(
                'emblem-synchronizing-symbolic',
                'Waiting for usage data',
                'The app is running and will update this view automatically.'));
        } else {
            for (const meter of METERS) {
                if (this._meterHasData(meter))
                    dashboard.add_child(this._createProviderCard(meter));
            }
        }

        item.add_child(dashboard);
        this._indicator.menu.addMenuItem(item);
    }

    _createHeader() {
        const status = connectionState(this._state);
        const subtitle = status === 'live'
            ? formatUpdatedTime(this._state?.lastUpdatedUnixMilliseconds ?? null)
            : status === 'syncing'
                ? 'Connecting to usage providers'
                : 'App is not running';
        const header = new St.BoxLayout({
            style_class: 'ccuc-header',
            x_expand: true,
            y_align: Clutter.ActorAlign.CENTER,
        });
        const titleBox = new St.BoxLayout({
            vertical: true,
            style_class: 'ccuc-header-copy',
            x_expand: true,
        });
        titleBox.add_child(new St.Label({
            text: 'Usage Companion',
            style_class: 'ccuc-title',
        }));
        titleBox.add_child(new St.Label({
            text: subtitle,
            style_class: 'ccuc-updated',
        }));
        header.add_child(titleBox);

        const statusText = status === 'live' ? 'LIVE' : status === 'syncing' ? 'SYNCING' : 'OFFLINE';
        header.add_child(new St.Label({
            text: statusText,
            style_class: `ccuc-status ccuc-status-${status}`,
            y_align: Clutter.ActorAlign.CENTER,
        }));
        return header;
    }

    _createEmptyState(iconName, title, description) {
        const box = new St.BoxLayout({
            vertical: true,
            style_class: 'ccuc-empty',
            x_align: Clutter.ActorAlign.CENTER,
            x_expand: true,
        });
        box.add_child(new St.Icon({
            icon_name: iconName,
            style_class: 'ccuc-empty-icon',
            x_align: Clutter.ActorAlign.CENTER,
        }));
        box.add_child(new St.Label({
            text: title,
            style_class: 'ccuc-empty-title',
            x_align: Clutter.ActorAlign.CENTER,
        }));
        box.add_child(new St.Label({
            text: description,
            style_class: 'ccuc-empty-description',
            x_align: Clutter.ActorAlign.CENTER,
        }));
        return box;
    }

    _createProviderCard(meter) {
        const fiveHourValue = this._state[meter.value];
        const weeklyValue = this._state[meter.weekly];
        const headlineValue = fiveHourValue ?? weeklyValue;
        const card = new St.BoxLayout({
            style_class: `ccuc-provider-card ccuc-provider-${meter.id}`,
            x_expand: true,
        });
        const overlay = new St.Widget({
            layout_manager: new Clutter.BinLayout(),
            width: 48,
            height: 48,
            y_align: Clutter.ActorAlign.START,
        });
        overlay.add_child(new RingMeter(headlineValue, meter.color, 48, 4));
        overlay.add_child(this._createProviderIcon(
            meter,
            'ccuc-provider-icon',
            'ccuc-provider-glyph'));
        card.add_child(overlay);

        const content = new St.BoxLayout({
            vertical: true,
            style_class: 'ccuc-provider-content',
            x_expand: true,
        });
        const titleRow = new St.BoxLayout({x_expand: true});
        titleRow.add_child(new St.Label({
            text: meter.name,
            style_class: 'ccuc-provider-name',
            x_expand: true,
        }));
        titleRow.add_child(new St.Label({
            text: formatPercent(headlineValue),
            style_class: 'ccuc-provider-value',
        }));
        content.add_child(titleRow);
        this._addLimit(
            content,
            '5-hour window',
            fiveHourValue,
            this._state[meter.reset],
            meter.color);
        this._addLimit(
            content,
            'Weekly limit',
            weeklyValue,
            this._state[meter.weeklyReset],
            meter.color);
        card.add_child(content);
        return card;
    }

    _meterHasData(meter) {
        return this._state?.[meter.enabled] === true &&
            (this._state[meter.value] !== null || this._state[meter.weekly] !== null);
    }

    _createProviderIcon(meter, iconStyleClass, fallbackStyleClass) {
        let file = null;
        if (meter.iconFile) {
            const candidate = this.dir.get_child('icons').get_child(meter.iconFile);
            if (candidate.query_exists(null))
                file = candidate;
        } else {
            file = meter.iconCandidates
                ?.map(path => Gio.File.new_for_path(path))
                .find(candidate => candidate.query_exists(null)) ?? null;
        }

        if (file) {
            return new St.Icon({
                gicon: new Gio.FileIcon({file}),
                style_class: iconStyleClass,
                x_align: Clutter.ActorAlign.CENTER,
                y_align: Clutter.ActorAlign.CENTER,
            });
        }

        return new St.Label({
            text: meter.fallbackGlyph,
            style_class: fallbackStyleClass,
            x_align: Clutter.ActorAlign.CENTER,
            y_align: Clutter.ActorAlign.CENTER,
        });
    }

    _addLimit(parent, name, value, resetAt, color) {
        const row = new St.BoxLayout({
            style_class: 'ccuc-limit-row',
            x_expand: true,
        });
        row.add_child(new St.Label({
            text: `${name}  ${formatPercent(value)}`,
            style_class: 'ccuc-limit-name',
            x_expand: true,
        }));
        row.add_child(new St.Label({
            text: formatResetTime(resetAt),
            style_class: 'ccuc-limit-reset',
        }));
        parent.add_child(row);
        parent.add_child(new BarMeter(value, color));
    }

    _runCompanion(command) {
        try {
            GLib.spawn_command_line_async(`${APPLICATION_COMMAND} ${command}`);
        } catch (error) {
            logError(error, `${this.metadata.uuid}: could not signal companion`);
        }
    }
}
