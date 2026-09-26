import { describe, expect, it } from 'vitest';
import { buildResources, SUPPORTED_LOCALES } from '.';

const REVIEW_KEY = '_frReview';

function flattenKeys(messages: Record<string, unknown>, prefix = ''): string[] {
  return Object.entries(messages).flatMap(([key, value]) => {
    if (key === REVIEW_KEY) return [];
    const path = prefix ? `${prefix}.${key}` : key;
    return value !== null && typeof value === 'object' && !Array.isArray(value)
      ? flattenKeys(value as Record<string, unknown>, path)
      : [path];
  });
}

describe('translation catalogs', () => {
  const resources = buildResources();
  const namespaces = new Set(Object.values(resources).flatMap((ns) => Object.keys(ns)));

  it('has a catalog for every supported locale', () => {
    expect(Object.keys(resources).sort()).toEqual([...SUPPORTED_LOCALES].sort());
  });

  it.each([...namespaces])('namespace "%s" has the same keys in fr-CA and en-CA', (namespace) => {
    const fr = flattenKeys(resources['fr-CA']?.[namespace] ?? {}).sort();
    const en = flattenKeys(resources['en-CA']?.[namespace] ?? {}).sort();
    expect(en).toEqual(fr);
  });

  it.each([...namespaces])(
    'namespace "%s" lists only existing keys for French review',
    (namespace) => {
      const fr = resources['fr-CA']?.[namespace] ?? {};
      const review = (fr[REVIEW_KEY] ?? []) as string[];
      const keys = new Set(flattenKeys(fr));
      expect(review.filter((key) => !keys.has(key))).toEqual([]);
    },
  );
});
