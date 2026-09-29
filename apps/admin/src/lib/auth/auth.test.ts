import { describe, expect, it } from 'vitest';
import { visibleModules } from '../../app/modules';
import { formatSharedKey } from './mfa';
import { afterSignIn, readAuthState, readFragment, stateAfterSignIn } from './navigation';
import { z } from 'zod';
import { newPasswordFields, passwordsMatch } from './passwords';
import { hasPermission } from './session';

describe('afterSignIn', () => {
  it.each([
    [undefined, '/'],
    ['/animals', '/animals'],
    ['/accept-invitation', '/accept-invitation'],
    ['/platform/staff?x=1', '/platform/staff?x=1'],
    // Never another origin.
    ['//evil.example', '/'],
    ['https://evil.example/', '/'],
    ['animals', '/'],
    // Never back to the sign-in pages.
    ['/login', '/'],
    ['/login?next=1', '/'],
    ['/login/mfa', '/'],
  ])('from %s goes to %s', (from, expected) => {
    expect(afterSignIn({ from })).toBe(expected);
  });
});

describe('readAuthState', () => {
  it('keeps only known, well-typed fields', () => {
    expect(readAuthState({ from: '/x', notice: 'signedOut', email: 'a@b.c', extra: 1 })).toEqual({
      from: '/x',
      notice: 'signedOut',
      email: 'a@b.c',
    });
    expect(readAuthState({ from: 3, notice: 'hacked' })).toEqual({
      from: undefined,
      notice: undefined,
      email: undefined,
    });
    expect(readAuthState(null)).toEqual({});
  });
});

describe('stateAfterSignIn', () => {
  it('carries only the invitation secret to the next page', () => {
    expect(
      stateAfterSignIn({ from: '/accept-invitation', invitationToken: 't', email: 'a@b.c' }),
    ).toEqual({
      invitationToken: 't',
    });
    expect(stateAfterSignIn({ from: '/animals', email: 'a@b.c' })).toBeUndefined();
  });
});

describe('readFragment', () => {
  it('reads parameters with or without the leading #', () => {
    expect(readFragment('#user=u1&token=a%2Bb').get('token')).toBe('a+b');
    expect(readFragment('token=t').get('token')).toBe('t');
    expect(readFragment('').get('token')).toBeNull();
  });
});

describe('formatSharedKey', () => {
  it('groups the key by four', () => {
    expect(formatSharedKey('JBSWY3DPEHPK3PXP')).toBe('JBSW Y3DP EHPK 3PXP');
    expect(formatSharedKey('ABCDE')).toBe('ABCD E');
    expect(formatSharedKey('AB CD EF')).toBe('ABCD EF');
  });
});

describe('new password schema', () => {
  const schema = z.object(newPasswordFields).refine(passwordsMatch.check, passwordsMatch.params);
  const issues = (password: string, confirmation: string) =>
    schema.safeParse({ password, confirmation }).error?.issues.map((i) => [i.path, i.message]);

  it('needs 12 characters, then a matching confirmation', () => {
    expect(issues('short', 'short')).toEqual([[['password'], 'platform:password.tooShort']]);
    expect(issues('long enough pass', 'different')).toEqual([
      [['confirmation'], 'platform:password.mismatch'],
    ]);
    expect(issues('long enough pass', 'long enough pass')).toBeUndefined();
  });

  it('keeps spaces: they are part of the password', () => {
    const parsed = schema.parse({ password: ' twelve chars ', confirmation: ' twelve chars ' });
    expect(parsed.password).toBe(' twelve chars ');
  });
});

describe('permissions in the UI', () => {
  it('hasPermission: null needs nothing; otherwise the session must hold it', () => {
    expect(hasPermission(undefined, null)).toBe(true);
    expect(hasPermission(undefined, 'animal.read')).toBe(false);
    expect(hasPermission({ permissions: ['animal.read'] }, 'animal.read')).toBe(true);
    expect(hasPermission({ permissions: ['animal.read'] }, 'platform.staff.read')).toBe(false);
  });

  it('visibleModules hides gated modules without their permission', () => {
    const keys = (permissions: string[]) => visibleModules(permissions).map((m) => m.key);

    expect(keys([])).toEqual([
      'people',
      'medical',
      'operations',
      'engagement',
      'municipal',
      'reporting',
    ]);
    expect(keys(['animal.read', 'movement.read', 'platform.staff.read'])).toHaveLength(9);
    expect(keys(['platform.staff.read'])).toContain('platform');
  });
});
