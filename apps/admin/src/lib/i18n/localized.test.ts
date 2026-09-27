import { describe, expect, it } from 'vitest';
import { localize } from './localized';

const dog = { fr: 'Chien', en: 'Dog' };

describe('localize', () => {
  it.each([
    ['fr-CA', 'Chien'],
    ['en-CA', 'Dog'],
    ['en', 'Dog'],
    ['EN-ca', 'Dog'],
    ['es-MX', 'Chien'],
    ['eng', 'Chien'],
    [undefined, 'Chien'],
  ])('resolves %s to %s', (locale, expected) => {
    expect(localize(dog, locale)).toBe(expected);
  });
});
