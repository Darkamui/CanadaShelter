import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import i18n from '../../../lib/i18n';
import { PingStatus } from './PingStatus';

function renderPingStatus() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <PingStatus />
      </QueryClientProvider>
    </I18nextProvider>,
  );
}

describe('PingStatus', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('fr-CA');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('calls the ping endpoint through the generated hook and shows connected', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(Response.json({ status: 'ok' }));
    vi.stubGlobal('fetch', fetchMock);

    renderPingStatus();

    expect(await screen.findByText('Serveur connecté')).toBeInTheDocument();
    expect(String(fetchMock.mock.calls[0]?.[0])).toContain('/api/platform/ping');
  });

  it('shows unreachable when the API fails', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn<typeof fetch>().mockResolvedValue(
        new Response(JSON.stringify({ title: 'Service Unavailable', status: 503 }), {
          status: 503,
          headers: { 'Content-Type': 'application/problem+json' },
        }),
      ),
    );

    renderPingStatus();

    expect(await screen.findByText('Serveur injoignable')).toBeInTheDocument();
  });
});
