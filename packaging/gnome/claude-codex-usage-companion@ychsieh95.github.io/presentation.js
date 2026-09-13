const DASH = '—';

function percent(value) {
    return Number.isInteger(value) && value >= 0 && value <= 100 ? value : null;
}

export function normalizeState(value) {
    if (!value || typeof value !== 'object' || value.schemaVersion !== 1) {
        return null;
    }

    return {
        hasAntigravity: value.hasAntigravity === true,
        geminiFiveHourRemaining: percent(value.geminiFiveHourRemaining),
        geminiWeeklyRemaining: percent(value.geminiWeeklyRemaining),
        claudeGptFiveHourRemaining: percent(value.claudeGptFiveHourRemaining),
        claudeGptWeeklyRemaining: percent(value.claudeGptWeeklyRemaining),
    };
}

export function formatPanelText(state) {
    if (!state?.hasAntigravity) {
        return `Antigravity ${DASH}`;
    }

    return `G ${formatPercent(state.geminiFiveHourRemaining)} · C ${formatPercent(state.claudeGptFiveHourRemaining)}`;
}

export function formatPercent(value) {
    return value === null ? DASH : `${value}%`;
}
