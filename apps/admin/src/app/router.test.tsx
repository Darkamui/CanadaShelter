import type { SessionResponse } from '@shelter/api-client/model';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import i18n from '../lib/i18n';
import { AppProviders } from './providers';
import { routes } from './router';

const ORG_A = '11111111-1111-1111-1111-111111111111';
const ORG_B = '22222222-2222-2222-2222-222222222222';

function session(overrides: Partial<SessionResponse> = {}): SessionResponse {
  return {
    user: {
      id: 'u1',
      displayName: 'Camille Tremblay',
      preferredLanguage: 'fr',
      isPlatformOperator: false,
      mfaEnabled: true,
    },
    memberships: [{ organizationId: ORG_A, organizationName: 'Refuge A' }],
    activeOrganizationId: ORG_A,
    permissions: ['animal.read', 'movement.read', 'platform.staff.read'],
    mfaEnrollmentRequired: false,
    ...overrides,
  };
}

const problem = (status: number) =>
  new Response(JSON.stringify({ status }), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  });

/** Routes fetch by path. `handlers` wins; anything else answers 200 `{status:'ok'}`. */
function mockApi(handlers: Record<string, () => Response>) {
  const fetchMock = vi.fn<typeof fetch>((input) => {
    const path = new URL(String(input), 'http://localhost').pathname;
    const handler = handlers[path];
    return Promise.resolve(handler ? handler() : Response.json({ status: 'ok' }));
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function renderAt(path: string) {
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  render(
    <AppProviders>
      <RouterProvider router={router} />
    </AppProviders>,
  );
  return router;
}

describe('route guards', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('fr-CA');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('sends a visitor without a session to the login page', async () => {
    mockApi({ '/api/platform/session': () => problem(401) });

    const router = renderAt('/animals');

    expect(await screen.findByRole('heading', { level: 1, name: 'Connexion' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/login');
    expect(router.state.location.state).toEqual({ from: '/animals' });
  });

  it('asks for an organization when several memberships and none active', async () => {
    mockApi({
      '/api/platform/session': () =>
        Response.json(
          session({
            activeOrganizationId: null,
            permissions: [],
            memberships: [
              { organizationId: ORG_A, organizationName: 'Refuge A' },
              { organizationId: ORG_B, organizationName: 'Refuge B' },
            ],
          }),
        ),
    });

    const router = renderAt('/');

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Choisir un organisme' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Refuge B' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/organizations');
  });

  it('forces MFA enrollment when the role requires it', async () => {
    mockApi({
      '/api/platform/session': () =>
        Response.json(
          session({
            permissions: [],
            mfaEnrollmentRequired: true,
            user: { ...session().user, mfaEnabled: false },
          }),
        ),
    });

    const router = renderAt('/platform/staff');

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Sécurisez votre compte' }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/mfa/enroll');
    expect(router.state.location.state).toEqual({ from: '/platform/staff' });
  });

  it('hides modules and pages the user has no permission for', async () => {
    mockApi({
      '/api/platform/session': () => Response.json(session({ permissions: ['animal.read'] })),
    });

    renderAt('/platform/staff');

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Accès refusé' }),
    ).toBeInTheDocument();
    const nav = screen.getByRole('navigation', { name: 'Navigation principale' });
    expect(within(nav).getByRole('link', { name: 'Animaux' })).toBeInTheDocument();
    expect(within(nav).queryByRole('link', { name: 'Plateforme' })).not.toBeInTheDocument();
    expect(within(nav).queryByRole('link', { name: 'Mouvements' })).not.toBeInTheDocument();
  });

  it('shows the staff page with its permission', async () => {
    mockApi({
      '/api/platform/session': () =>
        Response.json(session({ permissions: ['platform.staff.read'] })),
      '/api/platform/staff': () =>
        Response.json([
          {
            membershipId: 'm1',
            userId: 'u1',
            displayName: 'Camille Tremblay',
            email: 'camille@example.org',
            status: 'active',
            roles: ['administrator'],
          },
        ]),
      '/api/platform/invitations': () => Response.json([]),
    });

    renderAt('/platform');

    expect(await screen.findByRole('heading', { level: 1, name: 'Équipe' })).toBeInTheDocument();
    expect(await screen.findByRole('cell', { name: 'camille@example.org' })).toBeInTheDocument();
    // Read only: no management actions without platform.staff.manage.
    expect(screen.queryByRole('button', { name: /Suspendre/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('form', { name: 'Inviter' })).not.toBeInTheDocument();
  });

  it('brings an invitation through sign-in without putting its secret back in the URL', async () => {
    let signedIn = false;
    const lookupBodies: unknown[] = [];
    mockApi({
      '/api/platform/session': () => (signedIn ? Response.json(session()) : problem(401)),
      '/api/platform/session/login': () => {
        signedIn = true;
        return Response.json({ status: 'signedIn' });
      },
      '/api/platform/invitations/lookup': () =>
        Response.json({
          organizationName: 'Refuge B',
          email: 'camille@example.org',
          accountExists: true,
        }),
    });
    const fetchMock = vi.mocked(fetch);

    const router = renderAt('/accept-invitation#token=secret-token');

    fireEvent.click(await screen.findByRole('link', { name: 'Se connecter' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'Connexion' })).toBeInTheDocument();
    expect(screen.getByLabelText('Courriel')).toHaveValue('camille@example.org');
    fireEvent.change(screen.getByLabelText('Mot de passe'), {
      target: { value: 'correct horse battery' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Se connecter' }));

    expect(
      await screen.findByRole('button', { name: 'Accepter l’invitation' }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/accept-invitation');
    expect(router.state.location.hash).toBe('');
    for (const [input, init] of fetchMock.mock.calls) {
      if (String(input).endsWith('/invitations/lookup')) lookupBodies.push(init?.body);
    }
    expect(lookupBodies.at(-1)).toBe(JSON.stringify({ token: 'secret-token' }));
  });

  it('returns to the login page when an API call answers 401 mid-session', async () => {
    let signedIn = true;
    mockApi({
      '/api/platform/session': () => (signedIn ? Response.json(session()) : problem(401)),
      '/api/animals/species': () => {
        signedIn = false;
        return problem(401);
      },
    });

    const router = renderAt('/animals');

    expect(await screen.findByRole('heading', { level: 1, name: 'Connexion' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/login');
  });
});
