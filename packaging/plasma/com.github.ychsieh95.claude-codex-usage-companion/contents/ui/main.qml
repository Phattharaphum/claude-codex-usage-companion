// SPDX-License-Identifier: MIT

import QtQuick
import org.kde.kirigami as Kirigami
import org.kde.plasma.plasma5support as Plasma5Support
import org.kde.plasma.plasmoid

PlasmoidItem {
    id: root

    readonly property string statusCommand: "claude-codex-usage-companion status --json"
    readonly property string guiCommand: "claude-codex-usage-companion gui"
    property var claudeState: null
    property string claudeError: ""
    property bool claudePresent: false
    property var codexState: null
    property string codexError: ""
    property bool codexPresent: false
    property bool refreshing: false
    property string generalError: ""
    property date lastUpdated

    readonly property int lowestRemaining: calculateLowestRemaining()
    readonly property string panelText: lowestRemaining < 0 ? "--" : lowestRemaining.toString()
    readonly property color signalColor: colorForPercent(lowestRemaining)
    readonly property bool hasProviders: claudePresent || codexPresent
    readonly property bool anyLimitSelected: Plasmoid.configuration.showClaudeSession ||
                                             Plasmoid.configuration.showClaudeWeekly ||
                                             Plasmoid.configuration.showCodexFiveHour ||
                                             Plasmoid.configuration.showCodexWeekly
    readonly property var compactEntries: buildCompactEntries()

    Plasmoid.icon: "claude-codex-usage-companion"
    toolTipMainText: i18n("Claude Codex Usage")
    toolTipSubText: tooltipSummary()
    switchWidth: Kirigami.Units.gridUnit * 12
    switchHeight: Kirigami.Units.gridUnit * 10

    compactRepresentation: CompactRepresentation {
        controller: root
    }

    fullRepresentation: FullRepresentation {
        controller: root
    }

    Plasma5Support.DataSource {
        id: statusSource
        engine: "executable"

        onNewData: function(sourceName, data) {
            if (sourceName !== root.statusCommand) {
                return;
            }

            disconnectSource(sourceName);
            refreshTimeout.stop();
            root.refreshing = false;

            const output = data["stdout"] || "";
            if (output.trim().length === 0) {
                const detail = (data["stderr"] || "").trim();
                root.generalError = detail.length > 0
                    ? detail
                    : i18n("The companion command returned no data.");
                return;
            }

            try {
                root.applyPayload(JSON.parse(output));
                root.generalError = "";
                root.lastUpdated = new Date();
            } catch (error) {
                root.generalError = i18n("Could not read usage data: %1", error.message);
            }
        }
    }

    Plasma5Support.DataSource {
        id: commandRunner
        engine: "executable"

        onNewData: function(sourceName, data) {
            disconnectSource(sourceName);
        }
    }

    Timer {
        id: refreshTimer
        interval: Math.max(1, Math.min(60, Plasmoid.configuration.refreshIntervalMinutes)) * 60000
        repeat: true
        running: true
        onTriggered: root.refresh()
    }

    Timer {
        id: refreshTimeout
        interval: 30000
        repeat: false
        onTriggered: {
            statusSource.disconnectSource(root.statusCommand);
            root.refreshing = false;
            root.generalError = i18n("Usage refresh timed out.");
        }
    }

    function refresh() {
        if (refreshing) {
            return;
        }

        refreshing = true;
        generalError = "";
        statusSource.disconnectSource(statusCommand);
        statusSource.connectSource(statusCommand);
        refreshTimeout.restart();
    }

    function openCompanion() {
        commandRunner.disconnectSource(guiCommand);
        commandRunner.connectSource(guiCommand);
    }

    function applyPayload(payload) {
        claudePresent = Object.prototype.hasOwnProperty.call(payload, "claude");
        codexPresent = Object.prototype.hasOwnProperty.call(payload, "codex");

        const claude = claudePresent && payload.claude ? payload.claude : {};
        claudeState = claude.state || null;
        claudeError = claude.error || "";

        const codex = codexPresent && payload.codex ? payload.codex : {};
        codexState = codex.state || null;
        codexError = codex.error || "";
    }

    function calculateLowestRemaining() {
        const percentages = [];
        appendWindowPercentages(percentages, claudeState);
        appendWindowPercentages(percentages, codexState);
        if (percentages.length === 0) {
            return -1;
        }
        return Math.min.apply(Math, percentages);
    }

    function appendWindowPercentages(target, state) {
        if (!state) {
            return;
        }
        if (state.fiveHour && typeof state.fiveHour.remainingPercent === "number") {
            target.push(state.fiveHour.remainingPercent);
        }
        if (state.weekly && typeof state.weekly.remainingPercent === "number") {
            target.push(state.weekly.remainingPercent);
        }
    }

    function colorForPercent(percent) {
        if (percent < 0 || percent === 0) {
            return "#7f8c8d";
        }
        if (percent < 40) {
            return "#da4453";
        }
        if (percent < 60) {
            return "#f67400";
        }
        if (percent < 80) {
            return "#fdbc4b";
        }
        return "#27ae60";
    }

    function buildCompactEntries() {
        const entries = [];
        if (claudePresent) {
            if (Plasmoid.configuration.showClaudeSession) {
                entries.push(compactEntry(
                    "Cl·S",
                    i18n("Claude current session"),
                    claudeState ? claudeState.fiveHour : null));
            }
            if (Plasmoid.configuration.showClaudeWeekly) {
                entries.push(compactEntry(
                    "Cl·W",
                    i18n("Claude week (All)"),
                    claudeState ? claudeState.weekly : null));
            }
        }
        if (codexPresent) {
            if (Plasmoid.configuration.showCodexFiveHour) {
                entries.push(compactEntry(
                    "Cx·5",
                    i18n("Codex 5-hour limit"),
                    codexState ? codexState.fiveHour : null));
            }
            if (Plasmoid.configuration.showCodexWeekly) {
                entries.push(compactEntry(
                    "Cx·W",
                    i18n("Codex weekly limit"),
                    codexState ? codexState.weekly : null));
            }
        }
        return entries;
    }

    function compactEntry(shortLabel, title, windowState) {
        const available = windowState && typeof windowState.remainingPercent === "number";
        return {
            "shortLabel": shortLabel,
            "title": title,
            "percent": available ? windowState.remainingPercent : -1
        };
    }

    function tooltipSummary() {
        if (refreshing && !hasProviders) {
            return i18n("Refreshing usage…");
        }
        if (generalError.length > 0) {
            return generalError;
        }
        if (compactEntries.length > 0) {
            const summaries = [];
            for (let index = 0; index < compactEntries.length; index++) {
                const entry = compactEntries[index];
                summaries.push(entry.percent < 0
                    ? i18n("%1: unavailable", entry.title)
                    : i18n("%1: %2% remaining", entry.title, entry.percent));
            }
            return summaries.join("\n");
        }
        if (lowestRemaining >= 0) {
            return i18n("Lowest remaining limit: %1%", lowestRemaining);
        }
        if (hasProviders) {
            return i18n("Usage data is unavailable.");
        }
        return i18n("No usage providers are enabled.");
    }

    Component.onCompleted: refresh()
}
