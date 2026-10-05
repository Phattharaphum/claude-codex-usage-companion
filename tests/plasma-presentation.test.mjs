import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import test from 'node:test';

const ui = new URL('../packaging/plasma/com.github.ychsieh95.claude-codex-usage-companion/contents/ui/', import.meta.url);

function qmlFunction(file, name, variables = {}) {
    const source = readFileSync(new URL(file, ui), 'utf8');
    const match = source.match(new RegExp(`^    function ${name}\\([^)]*\\) \\{[\\s\\S]*?^    \\}`, 'm'));
    assert.ok(match, `Missing QML function ${name}`);
    return Function(...Object.keys(variables), `return (${match[0].trim()});`)(...Object.values(variables));
}

function i18n(format, ...values) {
    return values.reduce((result, value, index) => result.replace(`%${index + 1}`, value), format);
}

test('exhausted Plasma quotas are red and unavailable quotas stay neutral', () => {
    const color = qmlFunction('main.qml', 'colorForPercent');
    assert.equal(color(0), '#da4453');
    assert.equal(color(-1), '#7f8c8d');
    assert.equal(color(100), '#27ae60');
});

test('Plasma displays zero credits and formats balances to two decimal places', () => {
    const text = state => qmlFunction('ProviderSection.qml', 'supplementalText', {state, isClaude: false, i18n})();
    assert.equal(text({creditBalance: 0}), 'Credits: 0.00 · Automatic reload: Disabled');
    assert.equal(text({creditBalance: '23.7', automaticReloadEnabled: true}), 'Credits: 23.70 · Automatic reload: Enabled');
    assert.equal(text({creditBalance: null}), '');
    assert.equal(text({}), '');
});
