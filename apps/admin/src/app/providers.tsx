import { onUnauthorized } from '@shelter/api-client/http';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useEffect, useState, type ReactNode } from 'react';
import { I18nextProvider } from 'react-i18next';
import { sessionQueryKey, sessionUrl } from '../lib/auth/session';
import i18n from '../lib/i18n';

export function AppProviders({ children }: { children: ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false } },
      }),
  );

  // Any 401 means the session ended (expired, signed out elsewhere, stamp rotated): recheck it, and
  // `RequireSession` sends the user to the login page. The session request itself is skipped to avoid a loop.
  useEffect(
    () =>
      onUnauthorized((url) => {
        if (url !== sessionUrl) void queryClient.invalidateQueries({ queryKey: sessionQueryKey });
      }),
    [queryClient],
  );

  return (
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    </I18nextProvider>
  );
}
