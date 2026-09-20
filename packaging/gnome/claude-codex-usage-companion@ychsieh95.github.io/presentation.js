const DASH = '—';

function percent(value) {
    return Number.isInteger(value) && value >= 0 && value <= 100 ? value : null;
}

function timestamp(value) {
    return Number.isSafeInteger(value) && value >= 0 ? value : null;
}

function minimum(values) {
    const available = values.filter(value => value !== null);
    return available.length === 0 ? null : Math.min(...available);
}

export function normalizeState(value) {
    if (!value || typeof value !== 'object' ||
        ![1, 2, 3, 4].includes(value.schemaVersion)) {
        return null;
    }

    const geminiFiveHourRemaining = percent(value.geminiFiveHourRemaining);
    const geminiWeeklyRemaining = percent(value.geminiWeeklyRemaining);
    const claudeGptFiveHourRemaining = percent(value.claudeGptFiveHourRemaining);
    const claudeGptWeeklyRemaining = percent(value.claudeGptWeeklyRemaining);

    return {
        schemaVersion: value.schemaVersion,
        hasAntigravity: value.hasAntigravity === true,
        geminiFiveHourRemaining,
        geminiWeeklyRemaining,
        claudeGptFiveHourRemaining,
        claudeGptWeeklyRemaining,
        hasClaude: value.hasClaude === true,
        claudeFiveHourRemaining: percent(value.claudeFiveHourRemaining),
        claudeWeeklyRemaining: percent(value.claudeWeeklyRemaining),
        hasCodex: value.hasCodex === true,
        codexFiveHourRemaining: percent(value.codexFiveHourRemaining),
        codexWeeklyRemaining: percent(value.codexWeeklyRemaining),
        antigravityRemaining: percent(value.antigravityRemaining) ?? minimum([
            geminiFiveHourRemaining,
            claudeGptFiveHourRemaining,
        ]),
        antigravityWeeklyRemaining: percent(value.antigravityWeeklyRemaining) ?? minimum([
            geminiWeeklyRemaining,
            claudeGptWeeklyRemaining,
        ]),
        claudeFiveHourResetUnixMilliseconds:
            timestamp(value.claudeFiveHourResetUnixMilliseconds),
        claudeWeeklyResetUnixMilliseconds:
            timestamp(value.claudeWeeklyResetUnixMilliseconds),
        codexFiveHourResetUnixMilliseconds:
            timestamp(value.codexFiveHourResetUnixMilliseconds),
        codexWeeklyResetUnixMilliseconds:
            timestamp(value.codexWeeklyResetUnixMilliseconds),
        antigravityFiveHourResetUnixMilliseconds:
            timestamp(value.antigravityFiveHourResetUnixMilliseconds),
        antigravityWeeklyResetUnixMilliseconds:
            timestamp(value.antigravityWeeklyResetUnixMilliseconds),
        geminiFiveHourResetUnixMilliseconds:
            timestamp(value.geminiFiveHourResetUnixMilliseconds),
        geminiWeeklyResetUnixMilliseconds:
            timestamp(value.geminiWeeklyResetUnixMilliseconds),
        claudeGptFiveHourResetUnixMilliseconds:
            timestamp(value.claudeGptFiveHourResetUnixMilliseconds),
        claudeGptWeeklyResetUnixMilliseconds:
            timestamp(value.claudeGptWeeklyResetUnixMilliseconds),
        lastUpdatedUnixMilliseconds: timestamp(value.lastUpdatedUnixMilliseconds),
        publishedAtUnixMilliseconds: timestamp(value.publishedAtUnixMilliseconds),
    };
}

export function connectionState(state) {
    if (!state)
        return 'offline';

    const values = [
        state.hasClaude
            ? (state.claudeFiveHourRemaining ?? state.claudeWeeklyRemaining)
            : null,
        state.hasCodex
            ? (state.codexFiveHourRemaining ?? state.codexWeeklyRemaining)
            : null,
        state.hasAntigravity
            ? (state.geminiFiveHourRemaining ?? state.geminiWeeklyRemaining)
            : null,
        state.hasAntigravity
            ? (state.claudeGptFiveHourRemaining ?? state.claudeGptWeeklyRemaining)
            : null,
    ].filter(value => value !== null);

    return values.length === 0 ? 'syncing' : 'live';
}

export function formatPanelText(state) {
    const status = connectionState(state);
    if (status === 'offline')
        return '◌ App offline';
    if (status === 'syncing')
        return '↻ Syncing usage';

    const values = [
        state.hasClaude
            ? (state.claudeFiveHourRemaining ?? state.claudeWeeklyRemaining)
            : null,
        state.hasCodex
            ? (state.codexFiveHourRemaining ?? state.codexWeeklyRemaining)
            : null,
        state.hasAntigravity
            ? (state.geminiFiveHourRemaining ?? state.geminiWeeklyRemaining)
            : null,
        state.hasAntigravity
            ? (state.claudeGptFiveHourRemaining ?? state.claudeGptWeeklyRemaining)
            : null,
    ].filter(value => value !== null);
    return `Usage ${Math.min(...values)}%`;
}

export function formatPercent(value) {
    return value === null ? DASH : `${value}%`;
}

export function formatResetTime(value, now = Date.now()) {
    if (value === null)
        return 'Reset time unavailable';

    const remainingMinutes = Math.ceil((value - now) / 60_000);
    if (remainingMinutes <= 0)
        return 'Reset due now';
    if (remainingMinutes < 60)
        return `Resets in ${remainingMinutes}m`;

    const hours = Math.floor(remainingMinutes / 60);
    const minutes = remainingMinutes % 60;
    if (hours < 24)
        return minutes === 0 ? `Resets in ${hours}h` : `Resets in ${hours}h ${minutes}m`;

    const days = Math.floor(hours / 24);
    const remainingHours = hours % 24;
    return remainingHours === 0 ? `Resets in ${days}d` : `Resets in ${days}d ${remainingHours}h`;
}

export function formatUpdatedTime(value, now = Date.now()) {
    if (value === null)
        return 'Waiting for the first update';

    const elapsedMinutes = Math.max(0, Math.floor((now - value) / 60_000));
    if (elapsedMinutes < 1)
        return 'Updated just now';
    if (elapsedMinutes < 60)
        return `Updated ${elapsedMinutes}m ago`;

    const elapsedHours = Math.floor(elapsedMinutes / 60);
    return elapsedHours < 24
        ? `Updated ${elapsedHours}h ago`
        : `Updated ${Math.floor(elapsedHours / 24)}d ago`;
}
