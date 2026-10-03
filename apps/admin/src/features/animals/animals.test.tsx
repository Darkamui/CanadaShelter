import type {
  AnimalResponse,
  PagedResultOfAnimalListItem,
  PagedResultOfTimelineItem,
  SessionResponse,
} from '@shelter/api-client/model';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AppProviders } from '../../app/providers';
import { routes } from '../../app/router';
import i18n from '../../lib/i18n';

const ORG = '11111111-1111-1111-1111-111111111111';
const ANIMAL = '22222222-2222-2222-2222-222222222222';
const CHIP = '33333333-3333-3333-3333-333333333333';

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

const species = [
  { code: 'dog', label: { fr: 'Chien', en: 'Dog' } },
  { code: 'cat', label: { fr: 'Chat', en: 'Cat' } },
];

const animal: AnimalResponse = {
  id: ANIMAL,
  number: 12,
  name: 'Éclair',
  speciesCode: 'cat',
  breed: 'Européen',
  secondaryBreed: null,
  colour: 'Tigré',
  sex: 'female',
  reproductiveStatus: 'sterilized',
  birthDate: '2024-05-01',
  birthDateEstimated: true,
  marks: null,
  behaviourAlert: null,
  medicalAlert: 'Diabétique',
  legalAlert: null,
  custodyStatus: 'in_care',
  currentLocationId: '44444444-4444-4444-4444-444444444444',
  currentLocationName: 'Enclos 4',
  inCareSince: '2026-09-15T12:00:00Z',
  lastOutcomeCode: null,
  identifiers: [
    {
      id: CHIP,
      type: 'microchip',
      value: '900 123 456 789 012',
      isActive: true,
      createdAt: '2026-09-01T12:00:00Z',
      deactivatedAt: null,
    },
  ],
  createdAt: '2026-09-01T12:00:00Z',
  updatedAt: '2026-09-01T12:00:00Z',
  version: 7,
};

const page: PagedResultOfAnimalListItem = {
  items: [
    {
      id: ANIMAL,
      number: 12,
      name: 'Éclair',
      speciesCode: 'cat',
      breed: 'Européen',
      sex: 'female',
      custodyStatus: 'in_care',
      currentLocationId: animal.currentLocationId,
      currentLocationName: 'Enclos 4',
      inCareSince: animal.inCareSince,
      hasAlerts: true,
    },
  ],
  page: 1,
  pageSize: 25,
  totalCount: 1,
};

const timeline: PagedResultOfTimelineItem = {
  items: [
    {
      id: 't1',
      type: 'animal_registered',
      occurredAt: '2026-09-01T12:00:00Z',
      recordedAt: '2026-09-01T12:00:00Z',
      sourceModule: 'animals',
      sourceRecordId: null,
      parameters: {},
      names: {},
    },
  ],
  page: 1,
  pageSize: 20,
  totalCount: 1,
};

type Handler = (init?: RequestInit) => Response;

function mockApi(permissions: string[], handlers: Record<string, Handler>) {
  const all: Record<string, Handler> = {
    'GET /api/platform/session': () => Response.json(sessionWith(permissions)),
    'GET /api/animals/species': () => Response.json(species),
    [`GET /api/animals/${ANIMAL}`]: () => Response.json(animal),
    [`GET /api/animals/${ANIMAL}/timeline`]: () => Response.json(timeline),
    ...handlers,
  };
  const fetchMock = vi.fn<typeof fetch>((input, init) => {
    const url = new URL(String(input), 'http://localhost');
    const handler = all[`${init?.method ?? 'GET'} ${url.pathname}`];
    return Promise.resolve(handler ? handler(init) : Response.json({}, { status: 404 }));
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

const problem = (status: number, body: object) =>
  Response.json(
    { status, ...body },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  );

function renderAt(path: string) {
  render(
    <AppProviders>
      <RouterProvider router={createMemoryRouter(routes, { initialEntries: [path] })} />
    </AppProviders>,
  );
}

const reader = ['animal.read'];
const writer = ['animal.read', 'animal.write'];

describe('animals', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('fr-CA');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('lists animals with translated species and status, and sends the search to the server', async () => {
    const fetchMock = mockApi(reader, { 'GET /api/animals': () => Response.json(page) });
    renderAt('/animals');

    const link = await screen.findByRole('link', { name: 'Éclair' });
    const row = link.closest('tr')!;
    expect(within(row).getByText('#12')).toBeInTheDocument();
    expect(await within(row).findByText('Chat')).toBeInTheDocument();
    expect(within(row).getByText('Au refuge')).toBeInTheDocument();
    expect(within(row).getByText('Enclos 4')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Nouvel animal' })).not.toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Nom, numéro ou micropuce'), {
      target: { value: '900123' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Rechercher' }));
    await vi.waitFor(() =>
      expect(fetchMock.mock.calls.some(([input]) => String(input).includes('q=900123'))).toBe(true),
    );
  });

  it('requires a species, then creates the animal with blanks sent as null', async () => {
    let sent: unknown;
    const fetchMock = mockApi(writer, {
      'POST /api/animals': (init) => {
        sent = JSON.parse(String(init?.body));
        return Response.json(animal, { status: 201 });
      },
    });
    renderAt('/animals/new');

    const submit = await screen.findByRole('button', { name: 'Créer' });
    expect(screen.getByText(/N'inscrivez aucun renseignement personnel/)).toBeInTheDocument();
    fireEvent.click(submit);
    expect(await screen.findByText('Choisissez une espèce.')).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(false);

    fireEvent.change(screen.getByLabelText('Nom'), { target: { value: ' Éclair ' } });
    await screen.findByRole('option', { name: 'Chat' });
    fireEvent.change(screen.getByLabelText('Espèce'), { target: { value: 'cat' } });
    fireEvent.change(screen.getByLabelText('Micropuce'), {
      target: { value: '900 123 456 789 012' },
    });
    fireEvent.click(submit);

    expect(await screen.findByText("L'animal a été créé.")).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: '#12 Éclair' })).toBeInTheDocument();
    expect(sent).toMatchObject({
      name: 'Éclair',
      speciesCode: 'cat',
      breed: null,
      sex: 'unknown',
      reproductiveStatus: 'unknown',
      birthDate: null,
      birthDateEstimated: false,
      medicalAlert: null,
      microchip: '900 123 456 789 012',
    });
  });

  it('shows the custody summary, identifiers and timeline on the detail page', async () => {
    mockApi(writer, {});
    renderAt(`/animals/${ANIMAL}`);

    expect(
      await screen.findByRole('heading', { level: 1, name: '#12 Éclair' }),
    ).toBeInTheDocument();
    expect(screen.getByText('Au refuge')).toBeInTheDocument();
    expect(screen.getByText('900 123 456 789 012')).toBeInTheDocument();
    expect(await screen.findByText('Animal inscrit')).toBeInTheDocument();
    expect(screen.getByLabelText('Alerte médicale')).toHaveValue('Diabétique');
  });

  it('sends the version and reloads when someone else saved first', async () => {
    let sent: unknown;
    mockApi(writer, {
      [`PUT /api/animals/${ANIMAL}`]: (init) => {
        sent = JSON.parse(String(init?.body));
        return problem(409, { code: 'animal.versionConflict' });
      },
    });
    renderAt(`/animals/${ANIMAL}`);

    fireEvent.click(await screen.findByRole('button', { name: 'Enregistrer' }));

    expect(await screen.findByText(/Quelqu'un d'autre a modifié cet animal/)).toBeInTheDocument();
    expect(sent).toMatchObject({ version: 7, speciesCode: 'cat', birthDateEstimated: true });
  });

  it('shows a duplicate microchip on the identifier field', async () => {
    mockApi(writer, {
      [`POST /api/animals/${ANIMAL}/identifiers`]: () =>
        problem(400, { errors: { value: ['This microchip is already in use.'] } }),
    });
    renderAt(`/animals/${ANIMAL}`);

    fireEvent.change(await screen.findByLabelText('Numéro'), {
      target: { value: '900123456789012' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Ajouter' }));

    expect(
      await screen.findByText("Ce numéro n'est pas valide ou est déjà attribué à un autre animal."),
    ).toBeInTheDocument();
  });

  it('shows details read-only without animal.write', async () => {
    mockApi(reader, {});
    renderAt(`/animals/${ANIMAL}`);

    expect(await screen.findByText('Diabétique')).toBeInTheDocument();
    expect(screen.getByText('1 mai 2024 (estimée)')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Enregistrer' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Ajouter' })).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Désactiver 900 123 456 789 012' }),
    ).not.toBeInTheDocument();
  });
});
