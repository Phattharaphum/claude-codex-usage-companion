const DASH = '—';

function percent(value) {
    return Number.isInteger(value) && value >= 0 && value <= 100 ? value : null;
}

export function normalizeState(value) {
    if (!value || typeof value !== 'object' ||
        (value.schemaVersion !== 1 && value.schemaVersion !== 2)) {
        return null;
    }

    return {
        hasAntigravity: value.hasAntigravity === true,
        geminiFiveHourRemaining: percent(value.geminiFiveHourRemaining),
        geminiWeeklyRemaining: percent(value.geminiWeeklyRemaining),
        claudeGptFiveHourRemaining: percent(value.claudeGptFiveHourRemaining),
        claudeGptWeeklyRemaining: percent(value.claudeGptWeeklyRemaining),
        hasClaude: value.hasClaude === true,
        claudeFiveHourRemaining: percent(value.claudeFiveHourRemaining),
        hasCodex: value.hasCodex === true,
        codexFiveHourRemaining: percent(value.codexFiveHourRemaining),
        antigravityRemaining: percent(value.antigravityRemaining),
    };
}

export function formatPanelText(state) {
    const values = [
        state?.hasClaude ? state.claudeFiveHourRemaining : null,
        state?.hasCodex ? state.codexFiveHourRemaining : null,
        state?.hasAntigravity ? (state.antigravityRemaining ?? state.geminiFiveHourRemaining) : null,
    ].filter(value => value !== null);

    if (values.length === 0) {
        return `Usage ${DASH}`;
    }

    return `◉ ${Math.min(...values)}%`;
}

export function formatPercent(value) {
    return value === null ? DASH : `${value}%`;
}
