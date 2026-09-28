import type { SessionResponse } from '@shelter/api-client/model';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AppProviders } from '../../app/providers';
import { routes } from '../../app/router';
import i18n from '../../lib/i18n';

const ORG = '11111111-1111-1111-1111-111111111111';

const session: SessionResponse = {
  user: {
    id: 'u1',
    displayName: 'Camille Tremblay',
    preferredLanguage: 'fr',
    isPlatformOperator: false,
    mfaEnabled: true,
  },
  memberships: [{ organizationId: ORG, organizationName: 'Refuge A' }],
  activeOrganizationId: ORG,
  permissions: ['platform.staff.read', 'platform.staff.manage'],
  mfaEnrollmentRequired: false,
};

const json = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  });

function mockApi(handlers: Record<string, () => Response>) {
  const fetchMock = vi.fn<typeof fetch>((input) => {
    const path = new URL(String(input), 'http://localhost').pathname;
    const handler = handlers[path];
    return Promise.resolve(handler ? handler() : Response.json({ status: 'ok' }));
  });
  vi.stubGlobal('fetch', fetchMock);
  return (path: string) =>
    fetchMock.mock.calls.filter(([input]) => String(input).endsWith(path)).length;
}

function renderAt(path: string) {
  render(
    <AppProviders>
      <RouterProvider router={createMemoryRouter(routes, { initialEntries: [path] })} />
    </AppProviders>,
  );
}

describe('forms', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('fr-CA');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('checks a new password before sending it, then shows the server refusal on the field', async () => {
    const calls = mockApi({
      '/api/platform/session': () => json(401, { status: 401 }),
      '/api/platform/session/password/reset': () =>
        json(400, { status: 400, errors: { newPassword: ['Passwords must be unique.'] } }),
    });
    renderAt('/reset-password#user=u1&token=t1');

    const password = await screen.findByLabelText('Nouveau mot de passe');
    const confirmation = screen.getByLabelText('Confirmer le mot de passe');
    const submit = screen.getByRole('button', { name: 'Changer le mot de passe' });

    fireEvent.change(password, { target: { value: 'short' } });
    fireEvent.click(submit);
    expect(await screen.findByText('Le mot de passe est trop court.')).toBeInTheDocument();
    expect(password).toHaveAttribute('aria-invalid', 'true');

    fireEvent.change(password, { target: { value: 'long enough pass' } });
    fireEvent.change(confirmation, { target: { value: 'something else' } });
    fireEvent.click(submit);
    expect(await screen.findByText('Les mots de passe ne correspondent pas.')).toBeInTheDocument();
    expect(calls('/password/reset')).toBe(0);

    fireEvent.change(confirmation, { target: { value: 'long enough pass' } });
    fireEvent.click(submit);
    expect(
      await screen.findByText('Ce mot de passe ne respecte pas les exigences de sécurité.'),
    ).toBeInTheDocument();
    expect(calls('/password/reset')).toBe(1);
    // A field refusal keeps the form; only a refused link replaces it.
    expect(screen.queryByText('Ce lien est invalide ou expiré.')).not.toBeInTheDocument();
    expect(password).toHaveAttribute('aria-invalid', 'true');
  });

  it('says so when the server turns away too many attempts', async () => {
    mockApi({
      '/api/platform/session': () => json(401, { status: 401 }),
      '/api/platform/session/password/reset': () => json(429, { status: 429 }),
    });
    renderAt('/reset-password#user=u1&token=t1');

    fireEvent.change(await screen.findByLabelText('Nouveau mot de passe'), {
      target: { value: 'long enough pass' },
    });
    fireEvent.change(screen.getByLabelText('Confirmer le mot de passe'), {
      target: { value: 'long enough pass' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Changer le mot de passe' }));

    expect(
      await screen.findByText('Trop de tentatives. Réessayez dans quelques minutes.'),
    ).toBeInTheDocument();
    // Not mistaken for a refused link: the form stays.
    expect(screen.queryByText('Ce lien est invalide ou expiré.')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Nouveau mot de passe')).toBeInTheDocument();
  });

  it('asks for an email before requesting a reset link', async () => {
    const calls = mockApi({ '/api/platform/session': () => json(401, { status: 401 }) });
    renderAt('/forgot-password');

    fireEvent.change(await screen.findByLabelText('Courriel'), { target: { value: 'camille' } });
    fireEvent.click(screen.getByRole('button', { name: /lien/i }));

    expect(await screen.findByText('Entrez une adresse courriel valide.')).toBeInTheDocument();
    expect(calls('/password/forgot')).toBe(0);
  });

  it('needs at least one role to invite, then sends the trimmed values', async () => {
    let body: unknown;
    vi.stubGlobal(
      'fetch',
      vi.fn<typeof fetch>((input, init) => {
        const path = new URL(String(input), 'http://localhost').pathname;
        if (path === '/api/platform/session') return Promise.resolve(Response.json(session));
        if (path === '/api/platform/invitations' && init?.method === 'POST') {
          body = JSON.parse(String(init.body));
          return Promise.resolve(
            Response.json(
              { id: 'i1', email: 'alex@example.org', roles: ['read_only'], status: 'pending' },
              { status: 201 },
            ),
          );
        }
        return Promise.resolve(Response.json([]));
      }),
    );
    renderAt('/platform/staff');

    const form = await screen.findByRole('form', { name: 'Inviter' });
    fireEvent.change(within(form).getByLabelText('Courriel'), {
      target: { value: '  alex@example.org ' },
    });
    fireEvent.click(within(form).getByLabelText('Personnel'));
    fireEvent.click(within(form).getByRole('button', { name: 'Inviter' }));

    expect(await within(form).findByText('Choisissez au moins un rôle.')).toBeInTheDocument();
    expect(body).toBeUndefined();

    fireEvent.click(within(form).getByLabelText('Lecture seule'));
    fireEvent.click(within(form).getByRole('button', { name: 'Inviter' }));

    await waitFor(() =>
      expect(body).toEqual({ email: 'alex@example.org', roles: ['read_only'], language: 'fr' }),
    );
  });
});
