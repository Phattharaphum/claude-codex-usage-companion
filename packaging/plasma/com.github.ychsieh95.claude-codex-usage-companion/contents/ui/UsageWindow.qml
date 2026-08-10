// SPDX-License-Identifier: MIT

import QtQuick
import QtQuick.Layouts
import org.kde.kirigami as Kirigami
import org.kde.plasma.components as PlasmaComponents3

ColumnLayout {
    id: usageWindow

    required property string title
    required property var windowState
    required property var colorForPercent

    spacing: 2

    RowLayout {
        Layout.fillWidth: true

        PlasmaComponents3.Label {
            Layout.fillWidth: true
            text: usageWindow.title
            opacity: 0.85
            font.pointSize: Kirigami.Theme.smallFont.pointSize
        }

        PlasmaComponents3.Label {
            text: usageWindow.windowState
                ? i18n("%1% remaining", usageWindow.windowState.remainingPercent)
                : i18n("Unavailable")
            color: usageWindow.windowState
                ? usageWindow.colorForPercent(usageWindow.windowState.remainingPercent)
                : Kirigami.Theme.disabledTextColor
            font.bold: true
            font.pointSize: Kirigami.Theme.smallFont.pointSize
        }
    }

    Rectangle {
        Layout.fillWidth: true
        implicitHeight: Kirigami.Units.smallSpacing + 3
        radius: height / 2
        color: Qt.rgba(
            Kirigami.Theme.textColor.r,
            Kirigami.Theme.textColor.g,
            Kirigami.Theme.textColor.b,
            0.12)

        Rectangle {
            width: parent.width * usageWindow.fractionRemaining()
            height: parent.height
            radius: parent.radius
            color: usageWindow.windowState
                ? usageWindow.colorForPercent(usageWindow.windowState.remainingPercent)
                : "transparent"

            Behavior on width {
                NumberAnimation { duration: Kirigami.Units.shortDuration }
            }
        }
    }

    PlasmaComponents3.Label {
        Layout.fillWidth: true
        text: usageWindow.resetText()
        visible: text.length > 0
        opacity: 0.65
        elide: Text.ElideRight
        font.pointSize: Kirigami.Theme.smallFont.pointSize
    }

    function fractionRemaining() {
        if (!windowState || typeof windowState.remainingPercent !== "number") {
            return 0;
        }
        return Math.max(0, Math.min(1, windowState.remainingPercent / 100));
    }

    function resetText() {
        if (!windowState || windowState.resetsAt === null || windowState.resetsAt === undefined) {
            return "";
        }
        const reset = new Date(Number(windowState.resetsAt) * 1000);
        return i18n("Resets %1", reset.toLocaleString(Qt.locale(), Locale.ShortFormat));
    }
}
