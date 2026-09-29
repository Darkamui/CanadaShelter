import {
  getGetPlatformSessionQueryKey,
  getGetPlatformSessionUrl,
  useGetPlatformSession,
} from '@shelter/api-client/hooks/platform';
import { ApiError } from '@shelter/api-client/http';
import type { SessionResponse } from '@shelter/api-client/model';
import { hashKey, type QueryClient } from '@tanstack/react-query';

export const sessionQueryKey = getGetPlatformSessionQueryKey();
export const sessionUrl = getGetPlatformSessionUrl();

/** The signed-in user, memberships, active organization and permissions. A 401 means "not signed in". */
export function useSession() {
  return useGetPlatformSession({
    query: {
      retry: (failureCount, error) => !isUnauthorized(error) && failureCount < 1,
      staleTime: 60_000,
    },
  });
}

export function isUnauthorized(error: unknown): boolean {
  return error instanceof ApiError && error.status === 401;
}

/** The server's rate limiter turned the request away (429): too many attempts from this address. */
export function isRateLimited(error: unknown): boolean {
  return error instanceof ApiError && error.status === 429;
}

export function statusOf(error: unknown): number | undefined {
  return error instanceof ApiError ? error.status : undefined;
}

/** UX only: the server enforces every permission. `null` means the item needs none. */
export function hasPermission(
  session: Pick<SessionResponse, 'permissions'> | undefined,
  permission: string | null,
): boolean {
  return permission === null || (session?.permissions.includes(permission) ?? false);
}

export function activeOrganization(session: SessionResponse | undefined) {
  return session?.memberships.find((m) => m.organizationId === session.activeOrganizationId);
}

/**
 * After sign-in, sign-out, an organization switch or an MFA change: every cached response belonged to the
 * previous user or organization, so drop them all and reload the session. A loaded session is refetched in
 * place (the guards keep the current page mounted, e.g. to show recovery codes); a failed one (401 before
 * sign-in) is reset, so the guards wait for the new answer instead of redirecting on the stale error.
 */
export async function resetSession(queryClient: QueryClient): Promise<void> {
  const sessionHash = hashKey(sessionQueryKey);
  queryClient.removeQueries({ predicate: (query) => query.queryHash !== sessionHash });
  if (queryClient.getQueryState(sessionQueryKey)?.status === 'success') {
    await queryClient.refetchQueries({ queryKey: sessionQueryKey });
  } else {
    await queryClient.resetQueries({ queryKey: sessionQueryKey });
  }
}
