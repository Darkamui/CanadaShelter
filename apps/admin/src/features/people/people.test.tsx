import type {
  CreatePersonResponse,
  PagedResultOfPersonListItem,
  PersonResponse,
  SessionResponse,
} from '@shelter/api-client/model';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AppProviders } from '../../app/providers';
import { routes } from '../../app/router';
import i18n from '../../lib/i18n';

const ORG = '11111111-1111-1111-1111-111111111111';
const PERSON = '22222222-2222-2222-2222-222222222222';
const OTHER = '33333333-3333-3333-3333-333333333333';

function sessionWith(permissions: string[]): SessionResponse {
  return {
    user: {
      id: 'u1',
      displayName: 'Camille Tremblay',
      preferredLanguage: 'fr',
      isPlatformOperator: false,
      mfaEnabled: true,
    },
    memberships: [{ organizationId: ORG, organizationName: 'Refuge A' }],
    activeOrganizationId: ORG,
    permissions,
    mfaEnrollmentRequired: false,
  };
}

const person: PersonResponse = {
  id: PERSON,
  firstName: 'Éloïse',
  lastName: 'Gagnon',
  displayName: 'Éloïse Gagnon',
  email: 'eloise@example.test',
  phone: '514-555-0100',
  secondaryPhone: null,
  addressLine: null,
  city: 'Montréal',
  province: 'QC',
  postalCode: null,
  preferredLanguage: 'fr',
  roleTags: ['adopter', 'foster'],
  notes: null,
  isArchived: false,
  createdAt: '2026-09-01T12:00:00Z',
  updatedAt: '2026-09-01T12:00:00Z',
};

const page: PagedResultOfPersonListItem = {
  items: [
    {
      id: PERSON,
      displayName: person.displayName,
      email: person.email,
      phone: person.phone,
      city: person.city,
      roleTags: person.roleTags,
      isArchived: false,
    },
  ],
  page: 1,
  pageSize: 25,
  totalCount: 1,
};

type Handler = (init?: RequestInit) => Response;

function mockApi(handlers: Record<string, Handler>) {
  const fetchMock = vi.fn<typeof fetch>((input, init) => {
    const url = new URL(String(input), 'http://localhost');
    const handler = handlers[`${init?.method ?? 'GET'} ${url.pathname}`];
    return Promise.resolve(handler ? handler(init) : Response.json({}, { status: 404 }));
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function renderAt(path: string) {
  render(
    <AppProviders>
      <RouterProvider router={createMemoryRouter(routes, { initialEntries: [path] })} />
    </AppProviders>,
  );
}

describe('people', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('fr-CA');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('lists people with translated roles and sends the search to the server', async () => {
    const fetchMock = mockApi({
      'GET /api/platform/session': () => Response.json(sessionWith(['person.read'])),
      'GET /api/people': () => Response.json(page),
    });
    renderAt('/people');

    const link = await screen.findByRole('link', { name: 'Éloïse Gagnon' });
    const row = link.closest('tr');
    expect(row).not.toBeNull();
    expect(within(row!).getByText("Adoptant, Famille d'accueil")).toBeInTheDocument();
    // Read-only: no way to create.
    expect(screen.queryByRole('link', { name: 'Nouvelle personne' })).not.toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Nom, courriel ou téléphone'), {
      target: { value: 'eloise' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Rechercher' }));
    await vi.waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([input]) => String(input).includes('q=eloise')),
      ).toBeTruthy(),
    );
  });

  it('checks the name before sending, then shows possible duplicates after creating', async () => {
    let sent: unknown;
    const created: CreatePersonResponse = {
      person,
      possibleDuplicates: [{ id: OTHER, displayName: 'É. Gagnon', isArchived: true }],
    };
    const fetchMock = mockApi({
      'GET /api/platform/session': () =>
        Response.json(sessionWith(['person.read', 'person.write'])),
      'POST /api/people': (init) => {
        sent = JSON.parse(String(init?.body));
        return Response.json(created, { status: 201 });
      },
      [`GET /api/people/${PERSON}`]: () => Response.json(person),
    });
    renderAt('/people/new');

    const submit = await screen.findByRole('button', { name: 'Créer' });
    fireEvent.click(submit);
    expect(
      await screen.findByText('Entrez un prénom, un nom de famille ou un nom affiché.'),
    ).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(false);

    fireEvent.change(screen.getByLabelText('Prénom'), { target: { value: ' Éloïse ' } });
    fireEvent.change(screen.getByLabelText('Nom de famille'), { target: { value: 'Gagnon' } });
    fireEvent.click(screen.getByLabelText('Adoptant'));
    fireEvent.click(submit);

    expect(await screen.findByText('Doublons possibles')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'É. Gagnon' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Éloïse Gagnon' })).toBeInTheDocument();
    expect(sent).toMatchObject({
      firstName: 'Éloïse',
      lastName: 'Gagnon',
      displayName: null,
      email: null,
      preferredLanguage: 'fr',
      roleTags: ['adopter'],
    });
  });

  it('maps a server field error onto the form', async () => {
    mockApi({
      'GET /api/platform/session': () =>
        Response.json(sessionWith(['person.read', 'person.write'])),
      [`GET /api/people/${PERSON}`]: () => Response.json(person),
      [`PUT /api/people/${PERSON}`]: () =>
        Response.json(
          { status: 400, errors: { province: ['Unknown province.'] } },
          { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
        ),
    });
    renderAt(`/people/${PERSON}`);

    fireEvent.click(await screen.findByRole('button', { name: 'Enregistrer' }));
    expect(await screen.findByText("Cette valeur n'est pas valide.")).toBeInTheDocument();
  });

  it('shows details read-only without person.write', async () => {
    mockApi({
      'GET /api/platform/session': () => Response.json(sessionWith(['person.read'])),
      [`GET /api/people/${PERSON}`]: () => Response.json(person),
    });
    renderAt(`/people/${PERSON}`);

    expect(await screen.findByText('eloise@example.test')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Enregistrer' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Archiver' })).not.toBeInTheDocument();
  });
});
