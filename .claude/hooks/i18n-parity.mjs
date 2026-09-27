#!/usr/bin/env node
// PostToolUse: after an edit to an i18n catalog (…/i18n/fr-CA.json or en-CA.json), compare its keys with the
// sibling locale and tell Claude what is missing. Never blocks — Claude usually edits the other file next,
// and catalogs.test.ts is the real gate. Same flattening rules as apps/admin/src/lib/i18n/catalogs.test.ts.
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';

const REVIEW_KEY = '_frReview';
const SIBLING = { 'fr-CA.json': 'en-CA.json', 'en-CA.json': 'fr-CA.json' };

let raw = '';
process.stdin
  .on('data', (d) => (raw += d))
  .on('end', () => {
    try {
      const file = JSON.parse(raw).tool_input?.file_path ?? '';
      const name = path.basename(file);
      if (!SIBLING[name] || path.basename(path.dirname(file)) !== 'i18n') return;

      const other = path.join(path.dirname(file), SIBLING[name]);
      if (!existsSync(other))
        return report(
          `${SIBLING[name]} does not exist next to ${name}. Create it with the same keys.`,
        );

      const mine = new Set(flattenKeys(JSON.parse(readFileSync(file, 'utf8'))));
      const theirs = new Set(flattenKeys(JSON.parse(readFileSync(other, 'utf8'))));
      const missingThere = [...mine].filter((k) => !theirs.has(k));
      const missingHere = [...theirs].filter((k) => !mine.has(k));
      if (!missingThere.length && !missingHere.length) return;

      const lines = [`i18n catalogs out of sync in ${path.dirname(file)}:`];
      if (missingThere.length)
        lines.push(`  missing in ${SIBLING[name]}: ${missingThere.join(', ')}`);
      if (missingHere.length) lines.push(`  missing in ${name}: ${missingHere.join(', ')}`);
      report(lines.join('\n'));
    } catch {
      // invalid JSON mid-edit etc. — stay silent
    }
  });

function report(message) {
  process.stdout.write(
    JSON.stringify({
      hookSpecificOutput: { hookEventName: 'PostToolUse', additionalContext: message },
    }),
  );
}

function flattenKeys(messages, prefix = '') {
  return Object.entries(messages).flatMap(([key, value]) => {
    if (key === REVIEW_KEY) return [];
    const p = prefix ? `${prefix}.${key}` : key;
    return value !== null && typeof value === 'object' && !Array.isArray(value)
      ? flattenKeys(value, p)
      : [p];
  });
}
