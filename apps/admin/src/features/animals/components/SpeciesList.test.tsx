import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, render, screen } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import i18n from '../../../lib/i18n';
import { SpeciesList } from './SpeciesList';

const species = [
  { code: 'dog', label: { fr: 'Chien', en: 'Dog' } },
  { code: 'cat', label: { fr: 'Chat', en: 'Cat' } },
];

function renderSpeciesList() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <SpeciesList />
      </QueryClientProvider>
    </I18nextProvider>,
  );
}

describe('SpeciesList', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('fr-CA');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('switches labels with the UI locale without refetching', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(Response.json(species));
    vi.stubGlobal('fetch', fetchMock);

    renderSpeciesList();

    expect(await screen.findByText('Chien')).toBeInTheDocument();
    expect(screen.getByText('Chat')).toBeInTheDocument();
    expect(String(fetchMock.mock.calls[0]?.[0])).toContain('/api/animals/species');

    await act(() => i18n.changeLanguage('en-CA'));

    expect(await screen.findByText('Dog')).toBeInTheDocument();
    expect(screen.getByText('Cat')).toBeInTheDocument();
    expect(screen.queryByText('Chien')).not.toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('shows an error when the API fails', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn<typeof fetch>().mockResolvedValue(
        new Response(JSON.stringify({ title: 'Service Unavailable', status: 503 }), {
          status: 503,
          headers: { 'Content-Type': 'application/problem+json' },
        }),
      ),
    );

    renderSpeciesList();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Impossible de charger les espèces.',
    );
  });
});
