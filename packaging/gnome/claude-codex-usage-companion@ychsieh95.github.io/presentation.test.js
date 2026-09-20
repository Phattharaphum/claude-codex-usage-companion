import {
    connectionState,
    formatPanelText,
    formatResetTime,
    formatUpdatedTime,
    normalizeState,
} from './presentation.js';

function assertEquals(actual, expected) {
    if (actual !== expected)
        throw new Error(`Expected '${expected}', got '${actual}'`);
}

const bothPools = normalizeState({
    schemaVersion: 1,
    hasAntigravity: true,
    geminiFiveHourRemaining: 89,
    geminiWeeklyRemaining: 91,
    claudeGptFiveHourRemaining: 100,
    claudeGptWeeklyRemaining: 80,
});
assertEquals(formatPanelText(bothPools), 'Usage 89%');
assertEquals(bothPools.antigravityRemaining, 89);
assertEquals(bothPools.antigravityWeeklyRemaining, 80);

const missingClaude = normalizeState({
    schemaVersion: 1,
    hasAntigravity: true,
    geminiFiveHourRemaining: 89,
    claudeGptFiveHourRemaining: null,
});
assertEquals(formatPanelText(missingClaude), 'Usage 89%');
assertEquals(formatPanelText(null), '◌ App offline');
assertEquals(connectionState(null), 'offline');

const waiting = normalizeState({
    schemaVersion: 4,
    hasAntigravity: true,
});
assertEquals(connectionState(waiting), 'syncing');
assertEquals(formatPanelText(waiting), '↻ Syncing usage');

const fourMeters = normalizeState({
    schemaVersion: 4,
    hasClaude: true,
    claudeFiveHourRemaining: 73,
    claudeWeeklyRemaining: 62,
    hasCodex: true,
    codexFiveHourRemaining: 21,
    codexWeeklyRemaining: 48,
    hasAntigravity: true,
    geminiFiveHourRemaining: 52,
    geminiWeeklyRemaining: 74,
    claudeGptFiveHourRemaining: 64,
    claudeGptWeeklyRemaining: 83,
    claudeFiveHourResetUnixMilliseconds: 1_000_000,
    geminiFiveHourResetUnixMilliseconds: 2_000_000,
});
assertEquals(formatPanelText(fourMeters), 'Usage 21%');
assertEquals(fourMeters.claudeWeeklyRemaining, 62);
assertEquals(fourMeters.claudeFiveHourResetUnixMilliseconds, 1_000_000);
assertEquals(fourMeters.geminiFiveHourResetUnixMilliseconds, 2_000_000);

const oneAntigravityPool = normalizeState({
    schemaVersion: 4,
    hasAntigravity: true,
    geminiFiveHourRemaining: 67,
});
assertEquals(connectionState(oneAntigravityPool), 'live');
assertEquals(formatPanelText(oneAntigravityPool), 'Usage 67%');

assertEquals(formatResetTime(null, 0), 'Reset time unavailable');
assertEquals(formatResetTime(30 * 60_000, 0), 'Resets in 30m');
assertEquals(formatResetTime(125 * 60_000, 0), 'Resets in 2h 5m');
assertEquals(formatResetTime(51 * 60 * 60_000, 0), 'Resets in 2d 3h');
assertEquals(formatResetTime(0, 1), 'Reset due now');

assertEquals(formatUpdatedTime(null, 0), 'Waiting for the first update');
assertEquals(formatUpdatedTime(1_000, 30_000), 'Updated just now');
assertEquals(formatUpdatedTime(1_000, 181_000), 'Updated 3m ago');
