const test = require('node:test');
const assert = require('node:assert/strict');
const { load } = require('./load');

// ui.js is this app's own bootstrap (the platform has none): the helper that brings a chip in a
// sideways-scrolling row into view without moving the page.
test('revealChip: scrolls only as far as needed, on both axes', () => {
  const calls = [];
  load('ui.js', {}).appUi.revealChip({ scrollIntoView: opts => calls.push(opts) });
  assert.equal(JSON.stringify(calls), JSON.stringify([{ block: 'nearest', inline: 'nearest' }])); // built in the sandbox's realm
});

test('revealChip: a missing element, or one that cannot scroll, is ignored', () => {
  const { appUi } = load('ui.js', {});
  assert.doesNotThrow(() => appUi.revealChip(null));
  assert.doesNotThrow(() => appUi.revealChip({}));
});

test('appUi: helpers another script already put there are kept', () => {
  const existing = () => 'kept';
  const { appUi } = load('ui.js', { appUi: { existing } });
  assert.equal(appUi.existing, existing);
  assert.equal(typeof appUi.revealChip, 'function');
});
