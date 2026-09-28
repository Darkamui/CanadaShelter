import type { Location } from 'react-router';
import { paths } from './paths';

/**
 * What auth pages pass each other in router state (memory only, never in the URL or storage):
 * where to go after sign-in, a notice to show, an email to prefill, and an invitation secret carried through
 * sign-in (so it is never rebuilt into a URL).
 */
export interface AuthState {
  from?: string;
  notice?: 'passwordReset' | 'accountCreated' | 'signedOut';
  email?: string;
  invitationToken?: string;
}

const NOTICES = new Set<AuthState['notice']>(['passwordReset', 'accountCreated', 'signedOut']);

export function readAuthState(state: unknown): AuthState {
  if (typeof state !== 'object' || state === null) return {};
  const { from, notice, email, invitationToken } = state as Record<string, unknown>;
  return {
    from: typeof from === 'string' ? from : undefined,
    notice: NOTICES.has(notice as AuthState['notice'])
      ? (notice as AuthState['notice'])
      : undefined,
    email: typeof email === 'string' ? email : undefined,
    invitationToken: typeof invitationToken === 'string' ? invitationToken : undefined,
  };
}

/** The current location as a `from` value. */
export function currentPath(location: Pick<Location, 'pathname' | 'search' | 'hash'>): string {
  return `${location.pathname}${location.search}${location.hash}`;
}

/** Where to land after sign-in: an in-app path from state, never the login pages or another origin. */
export function afterSignIn(state: AuthState): string {
  const from = state.from;
  if (!from?.startsWith('/') || from.startsWith('//')) return paths.home;
  const pathname = from.split(/[?#]/, 1)[0];
  return pathname === paths.login || pathname === paths.loginMfa ? paths.home : from;
}

/** Router state for the page reached after sign-in: only what that page still needs. */
export function stateAfterSignIn(state: AuthState): AuthState | undefined {
  return state.invitationToken ? { invitationToken: state.invitationToken } : undefined;
}

/** Reads `a=1&b=2` from a URL fragment (links put secrets there so they never reach a server). */
export function readFragment(hash: string): URLSearchParams {
  return new URLSearchParams(hash.startsWith('#') ? hash.slice(1) : hash);
}
