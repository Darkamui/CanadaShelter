import { ApiError } from '@shelter/api-client/http';
import { describe, expect, it, vi } from 'vitest';
import { z } from 'zod';
import en from '../../app/i18n/en-CA.json';
import fr from '../../app/i18n/fr-CA.json';
import { emailAddress, formMessages, requiredText } from './schemas';
import { applyValidationErrors, validationErrors } from './serverErrors';

describe('shared field schemas', () => {
  it('requiredText trims, then needs a character', () => {
    expect(requiredText().parse('  Camille ')).toBe('Camille');
    expect(requiredText().safeParse('   ').error?.issues[0]?.message).toBe(formMessages.required);
    expect(requiredText('platform:x').safeParse('').error?.issues[0]?.message).toBe('platform:x');
  });

  it('emailAddress: blank is required, malformed is invalid', () => {
    const message = (value: string) => emailAddress().safeParse(value).error?.issues[0]?.message;
    expect(emailAddress().parse(' camille@example.org ')).toBe('camille@example.org');
    expect(message('')).toBe(formMessages.required);
    expect(message('camille')).toBe(formMessages.email);
  });

  it('every shared message exists in both shell catalogs', () => {
    for (const key of Object.values(formMessages)) {
      const [, path] = key.split(':') as [string, string];
      const [group, name] = path.split('.') as [string, string];
      for (const catalog of [fr, en] as unknown as Record<string, Record<string, string>>[]) {
        expect(catalog[group]?.[name]).toBeTruthy();
      }
    }
  });

  it('reports the field path inside an object', () => {
    const schema = z.object({ name: requiredText() });
    expect(schema.safeParse({ name: '' }).error?.issues[0]?.path).toEqual(['name']);
  });
});

describe('server validation errors', () => {
  const validation = new ApiError(400, { status: 400, errors: { newPassword: ['Too common.'] } });

  it('reads only a 400 with field errors', () => {
    expect(validationErrors(validation)).toEqual({ newPassword: ['Too common.'] });
    expect(validationErrors(new ApiError(400, { status: 400, title: 'Bad link' }))).toBeUndefined();
    expect(validationErrors(new ApiError(409, { status: 409 }))).toBeUndefined();
    expect(validationErrors(new Error('network'))).toBeUndefined();
  });

  it('puts a catalog key on the mapped field, focusing only the first', () => {
    const setError = vi.fn();
    const both = new ApiError(400, {
      errors: { DisplayName: ['x'], password: ['y'], token: ['z'] },
    });

    const applied = applyValidationErrors<{ displayName: string; password: string }>(
      both,
      setError,
      {
        displayName: ['displayName', 'platform:accept.nameRequired'],
        password: ['password', 'platform:password.rejected'],
      },
    );

    expect(applied).toBe(true);
    expect(setError.mock.calls).toEqual([
      [
        'displayName',
        { type: 'server', message: 'platform:accept.nameRequired' },
        { shouldFocus: true },
      ],
      [
        'password',
        { type: 'server', message: 'platform:password.rejected' },
        { shouldFocus: false },
      ],
    ]);
  });

  it('reports false when nothing matched, so the form shows an alert', () => {
    const setError = vi.fn();
    expect(applyValidationErrors(new ApiError(409, {}), setError, {})).toBe(false);
    expect(applyValidationErrors(validation, setError, {})).toBe(false);
    expect(setError).not.toHaveBeenCalled();
  });
});
