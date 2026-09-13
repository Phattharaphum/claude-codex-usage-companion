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
assertEquals(formatPanelText(bothPools), '◉ 89%');

const missingClaude = normalizeState({
    schemaVersion: 1,
    hasAntigravity: true,
    geminiFiveHourRemaining: 89,
    claudeGptFiveHourRemaining: null,
});
assertEquals(formatPanelText(missingClaude), '◉ 89%');
assertEquals(formatPanelText(null), 'Usage —');

const threeProviders = normalizeState({
    schemaVersion: 2,
    hasClaude: true,
    claudeFiveHourRemaining: 73,
    hasCodex: true,
    codexFiveHourRemaining: 21,
    hasAntigravity: true,
    antigravityRemaining: 52,
});
assertEquals(formatPanelText(threeProviders), '◉ 21%');
