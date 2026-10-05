import {
    connectionState,
    formatPanelText,
    formatResetTime,
    formatUpdatedTime,
    normalizeState,
    StateReader,
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

function pendingRead() {
    let resolve;
    let reject;
    const promise = new Promise((success, failure) => {
        resolve = success;
        reject = failure;
    });
    return {promise, resolve, reject};
}

function contents(percent) {
    return [new TextEncoder().encode(JSON.stringify({
        schemaVersion: 4,
        hasCodex: true,
        codexFiveHourRemaining: percent,
    }))];
}

// The slower, older read must never replace a newer snapshot.
const applied = [];
const reader = new StateReader(state => applied.push(state));
const older = pendingRead();
const newer = pendingRead();
const olderReload = reader.reload({load_contents_async: () => older.promise});
const newerReload = reader.reload({load_contents_async: () => newer.promise});
newer.resolve(contents(27));
await newerReload;
older.resolve(contents(86));
await olderReload;
assertEquals(applied.length, 1);
assertEquals(applied[0].codexFiveHourRemaining, 27);

// Disabling during a read must suppress presentation and further file access.
for (const shouldFail of [false, true]) {
    const pending = pendingRead();
    const disposedReader = new StateReader(() => {
        throw new Error('A disabled reader updated presentation');
    });
    const reload = disposedReader.reload({
        load_contents_async: () => pending.promise,
        query_exists: () => {
            throw new Error('A disabled reader queried a disposed file');
        },
    });
    disposedReader.dispose();
    if (shouldFail)
        pending.reject(new Error('Read was cancelled'));
    else
        pending.resolve(contents(50));
    await reload;
}

// A transient read failure preserves state; removing the file marks it offline.
await reader.reload({
    load_contents_async: async () => { throw new Error('Atomic replacement'); },
    query_exists: () => true,
});
assertEquals(applied.length, 1);
await reader.reload({
    load_contents_async: async () => { throw new Error('File was removed'); },
    query_exists: () => false,
});
assertEquals(applied.length, 2);
assertEquals(applied[1], null);
