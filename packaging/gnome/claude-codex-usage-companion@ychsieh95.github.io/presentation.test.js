import {formatPanelText, normalizeState} from './presentation.js';

function assertEquals(actual, expected) {
    if (actual !== expected)
        throw new Error(`Expected '${expected}', got '${actual}'`);
}

const bothPools = normalizeState({
    schemaVersion: 1,
    hasAntigravity: true,
    geminiFiveHourRemaining: 89,
    claudeGptFiveHourRemaining: 100,
});
assertEquals(formatPanelText(bothPools), 'G 89% · C 100%');

const missingClaude = normalizeState({
    schemaVersion: 1,
    hasAntigravity: true,
    geminiFiveHourRemaining: 89,
    claudeGptFiveHourRemaining: null,
});
assertEquals(formatPanelText(missingClaude), 'G 89% · C —');
assertEquals(formatPanelText(null), 'Antigravity —');
