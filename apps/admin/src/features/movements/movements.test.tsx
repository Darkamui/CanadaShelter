import type {
  AnimalResponse,
  MovementItem,
  PagedResultOfPersonListItem,
  PagedResultOfTimelineItem,
  SessionResponse,
} from '@shelter/api-client/model';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AppProviders } from '../../app/providers';
import { routes } from '../../app/router';
import i18n from '../../lib/i18n';
import { latestInEffect } from './model';

const ORG = '11111111-1111-1111-1111-111111111111';
const ANIMAL = '22222222-2222-2222-2222-222222222222';
const BUILDING = '33333333-3333-3333-3333-333333333333';
const KENNEL_1 = '44444444-4444-4444-4444-444444444444';
const KENNEL_2 = '55555555-5555-5555-5555-555555555555';
const PERSON = '66666666-6666-6666-6666-666666666666';
const INTAKE = '77777777-7777-7777-7777-777777777777';
const MOVE = '88888888-8888-8888-8888-888888888888';

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

const notInCare: AnimalResponse = {
  id: ANIMAL,
  number: 12,
  name: 'Éclair',
  speciesCode: 'cat',
  breed: null,
  secondaryBreed: null,
  colour: null,
  sex: 'female',
  reproductiveStatus: 'unknown',
  birthDate: null,
  birthDateEstimated: false,
  marks: null,
  behaviourAlert: null,
  medicalAlert: null,
  legalAlert: null,
  custodyStatus: 'not_in_care',
  currentLocationId: null,
  currentLocationName: null,
  inCareSince: null,
  lastOutcomeCode: null,
  identifiers: [],
  createdAt: '2026-09-01T12:00:00Z',
  updatedAt: '2026-09-01T12:00:00Z',
  version: 1,
};

const inCare: AnimalResponse = {
  ...notInCare,
  custodyStatus: 'in_care',
  currentLocationId: KENNEL_2,
  currentLocationName: 'Enclos 2',
  inCareSince: '2026-09-15T12:00:00Z',
};

const movement = (overrides: Partial<MovementItem>): MovementItem => ({
  id: INTAKE,
  type: 'intake',
  animalId: ANIMAL,
  reasonCode: null,
  fromLocationId: null,
  fromLocationName: null,
  toLocationId: null,
  toLocationName: null,
  personId: null,
  personName: null,
  notes: null,
  occurredAt: '2026-09-15T12:00:00Z',
  recordedAt: '2026-09-15T12:00:00Z',
  voidsMovementId: null,
  voidedByMovementId: null,
  ...overrides,
});

// Newest first, as the server sends them.
const history: MovementItem[] = [
  movement({
    id: MOVE,
    type: 'relocation',
    fromLocationId: KENNEL_1,
    fromLocationName: 'Enclos 1',
    toLocationId: KENNEL_2,
    toLocationName: 'Enclos 2',
    occurredAt: '2026-09-16T12:00:00Z',
  }),
  movement({
    reasonCode: 'stray',
    toLocationId: KENNEL_1,
    toLocationName: 'Enclos 1',
    personId: PERSON,
    personName: 'Léa Gagnon',
  }),
];

const timeline: PagedResultOfTimelineItem = {
  items: [
    {
      id: 't2',
      type: 'intake_recorded',
      occurredAt: '2026-09-15T12:00:00Z',
      recordedAt: '2026-09-15T12:00:00Z',
      sourceModule: 'movements',
      sourceRecordId: INTAKE,
      parameters: { reasonCode: 'stray', toLocationId: KENNEL_1 },
      names: { toLocationId: 'Enclos 1' },
    },
  ],
  page: 1,
  pageSize: 20,
  totalCount: 1,
};

const people: PagedResultOfPersonListItem = {
  items: [
    {
      id: PERSON,
      displayName: 'Léa Gagnon',
      email: 'lea@example.com',
      phone: null,
      city: null,
      roleTags: ['adopter'],
      isArchived: false,
    },
  ],
  page: 1,
  pageSize: 10,
  totalCount: 1,
};

type Handler = (init?: RequestInit) => Response;

function mockApi(
  permissions: string[],
  animal: AnimalResponse,
  handlers: Record<string, Handler> = {},
) {
  const all: Record<string, Handler> = {
    'GET /api/platform/session': () => Response.json(sessionWith(permissions)),
    'GET /api/animals/species': () =>
      Response.json([{ code: 'cat', label: { fr: 'Chat', en: 'Cat' } }]),
    [`GET /api/animals/${ANIMAL}`]: () => Response.json(animal),
    [`GET /api/animals/${ANIMAL}/timeline`]: () => Response.json(timeline),
    'GET /api/movements': () => Response.json(animal === notInCare ? [] : history),
    'GET /api/movements/intake-reasons': () =>
      Response.json([
        { code: 'stray', label: { fr: 'Errant', en: 'Stray' } },
        { code: 'surrender', label: { fr: 'Abandon par le propriétaire', en: 'Owner surrender' } },
      ]),
    'GET /api/movements/outcome-types': () =>
      Response.json([
        { code: 'adoption', label: { fr: 'Adoption', en: 'Adoption' }, requiresPerson: true },
        { code: 'died', label: { fr: 'Décès', en: 'Died' }, requiresPerson: false },
      ]),
    'GET /api/operations/locations': () =>
      Response.json([
        {
          id: BUILDING,
          parentId: null,
          kindCode: 'building',
          name: 'Pavillon A',
          capacity: null,
          isArchived: false,
        },
        {
          id: KENNEL_1,
          parentId: BUILDING,
          kindCode: 'kennel',
          name: 'Enclos 1',
          capacity: 1,
          isArchived: false,
        },
        {
          id: KENNEL_2,
          parentId: BUILDING,
          kindCode: 'kennel',
          name: 'Enclos 2',
          capacity: 1,
          isArchived: false,
        },
      ]),
    'GET /api/operations/location-kinds': () =>
      Response.json([
        { code: 'building', label: { fr: 'Bâtiment', en: 'Building' }, holdsAnimals: false },
        { code: 'kennel', label: { fr: 'Enclos', en: 'Kennel' }, holdsAnimals: true },
      ]),
    'GET /api/people': () => Response.json(people),
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

const reads = ['animal.read', 'movement.read', 'location.read', 'person.read'];
const staff = [...reads, 'animal.write', 'movement.write'];
const administrator = [...staff, 'movement.amend'];

describe('movements', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('fr-CA');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('requires a reason and a location, then records the intake with a person', async () => {
    let sent: unknown;
    const fetchMock = mockApi(staff, notInCare, {
      'POST /api/movements/intakes': (init) => {
        sent = JSON.parse(String(init?.body));
        return Response.json(movement({}), { status: 201 });
      },
    });
    renderAt(`/animals/${ANIMAL}`);

    expect(await screen.findByText('Aucun mouvement.')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Enregistrer une admission' }));
    const dialog = await screen.findByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: "Enregistrer l'admission" }));
    expect(await within(dialog).findAllByText('Ce champ est obligatoire.')).toHaveLength(2);

    fireEvent.change(within(dialog).getByLabelText("Motif d'admission"), {
      target: { value: 'stray' },
    });
    // Only locations that hold animals, labelled with their path.
    const to = within(dialog).getByLabelText('Vers');
    expect(within(to).queryByText('Pavillon A')).not.toBeInTheDocument();
    fireEvent.change(to, { target: { value: KENNEL_1 } });
    expect(within(to).getByText('Pavillon A › Enclos 1')).toBeInTheDocument();
    fireEvent.change(within(dialog).getByLabelText('Rechercher par nom, courriel ou téléphone'), {
      target: { value: 'léa' },
    });
    const picker = within(dialog).getByLabelText('Personnes trouvées');
    await within(picker).findByText('Léa Gagnon · lea@example.com');
    fireEvent.change(picker, { target: { value: PERSON } });
    fireEvent.click(within(dialog).getByRole('button', { name: "Enregistrer l'admission" }));

    expect(await screen.findByText("L'admission a été enregistrée.")).toBeInTheDocument();
    expect(sent).toEqual({
      animalId: ANIMAL,
      reasonCode: 'stray',
      toLocationId: KENNEL_1,
      personId: PERSON,
      notes: null,
      occurredAt: null,
    });
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    // The animal is reloaded for its new custody summary.
    expect(
      fetchMock.mock.calls.filter(([input]) => String(input).endsWith(`/api/animals/${ANIMAL}`))
        .length,
    ).toBeGreaterThan(1);
  });

  it('shows a custody conflict on the dialog', async () => {
    mockApi(staff, notInCare, {
      'POST /api/movements/intakes': () => problem(409, { code: 'movement.custodyConflict' }),
    });
    renderAt(`/animals/${ANIMAL}`);

    fireEvent.click(await screen.findByRole('button', { name: 'Enregistrer une admission' }));
    const dialog = await screen.findByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText("Motif d'admission"), {
      target: { value: 'stray' },
    });
    await within(dialog).findByText('Pavillon A › Enclos 1');
    fireEvent.change(within(dialog).getByLabelText('Vers'), { target: { value: KENNEL_1 } });
    fireEvent.click(within(dialog).getByRole('button', { name: "Enregistrer l'admission" }));

    expect(
      await within(dialog).findByText(/La garde de l'animal a changé entre-temps/),
    ).toBeInTheDocument();
  });

  it('requires a person for an adoption', async () => {
    const fetchMock = mockApi(staff, inCare);
    renderAt(`/animals/${ANIMAL}`);

    fireEvent.click(await screen.findByRole('button', { name: 'Enregistrer une sortie' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('Quitte Enclos 2.')).toBeInTheDocument();
    await within(dialog).findByText('Adoption');
    fireEvent.change(within(dialog).getByLabelText('Type de sortie'), {
      target: { value: 'adoption' },
    });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Enregistrer la sortie' }));

    expect(await within(dialog).findByText('Ce champ est obligatoire.')).toBeInTheDocument();
    expect(
      fetchMock.mock.calls.some(([input]) => String(input).includes('/api/movements/outcomes')),
    ).toBe(false);
  });

  it('moves an animal to another location, never its current one', async () => {
    let sent: unknown;
    mockApi(staff, inCare, {
      'POST /api/movements/relocations': (init) => {
        sent = JSON.parse(String(init?.body));
        return Response.json(movement({ id: MOVE, type: 'relocation' }), { status: 201 });
      },
    });
    renderAt(`/animals/${ANIMAL}`);

    fireEvent.click(await screen.findByRole('button', { name: 'Déplacer' }));
    const dialog = await screen.findByRole('dialog');
    const to = within(dialog).getByLabelText('Vers');
    await within(to).findByText('Pavillon A › Enclos 1');
    expect(within(to).queryByText('Pavillon A › Enclos 2')).not.toBeInTheDocument();
    fireEvent.change(to, { target: { value: KENNEL_1 } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Enregistrer le déplacement' }));

    expect(await screen.findByText('Le déplacement a été enregistré.')).toBeInTheDocument();
    expect(sent).toEqual({
      animalId: ANIMAL,
      toLocationId: KENNEL_1,
      notes: null,
      occurredAt: null,
    });
  });

  it('shows the history, with void only on the latest movement and only with movement.amend', async () => {
    mockApi(staff, inCare);
    renderAt(`/animals/${ANIMAL}`);

    const history = await screen.findByRole('region', { name: 'Mouvements' });
    expect(await within(history).findByText('Déplacement')).toBeInTheDocument();
    expect(within(history).getByText('Admission')).toBeInTheDocument();
    expect(await within(history).findByText('Errant')).toBeInTheDocument();
    expect(within(history).getByText('Léa Gagnon')).toBeInTheDocument();
    expect(within(history).queryByRole('button', { name: 'Annuler' })).not.toBeInTheDocument();
  });

  it('voids the latest movement with a reason', async () => {
    let sent: unknown;
    mockApi(administrator, inCare, {
      [`POST /api/movements/${MOVE}/void`]: (init) => {
        sent = JSON.parse(String(init?.body));
        return Response.json(movement({ id: 'v1', type: 'void', voidsMovementId: MOVE }), {
          status: 201,
        });
      },
    });
    renderAt(`/animals/${ANIMAL}`);

    const history = await screen.findByRole('region', { name: 'Mouvements' });
    const voidButtons = await within(history).findAllByRole('button', { name: 'Annuler' });
    expect(voidButtons).toHaveLength(1);
    fireEvent.click(voidButtons[0]!);
    const dialog = await screen.findByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Annuler le mouvement' }));
    expect(await within(dialog).findByText('Ce champ est obligatoire.')).toBeInTheDocument();
    fireEvent.change(within(dialog).getByLabelText('Motif'), {
      target: { value: 'Mauvais enclos' },
    });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Annuler le mouvement' }));

    expect(await screen.findByText('Le mouvement a été annulé.')).toBeInTheDocument();
    expect(sent).toEqual({ reason: 'Mauvais enclos' });
  });

  it('labels movement events on the timeline', async () => {
    mockApi(reads, inCare);
    renderAt(`/animals/${ANIMAL}`);

    const panel = await screen.findByRole('region', { name: 'Historique' });
    expect(await within(panel).findByText('Admission enregistrée')).toBeInTheDocument();
    expect(await within(panel).findByText('Errant')).toBeInTheDocument();
    expect(within(panel).getByText('Enclos 1')).toBeInTheDocument();
    // No custody actions without movement.write.
    expect(screen.queryByRole('button', { name: 'Déplacer' })).not.toBeInTheDocument();
  });

  it('opens the intake dialog after creating an animal', async () => {
    mockApi(staff, notInCare, {
      'POST /api/animals': () => Response.json(notInCare, { status: 201 }),
    });
    renderAt('/animals/new');

    expect(await screen.findByLabelText("Enregistrer l'admission ensuite")).toBeChecked();
    fireEvent.change(screen.getByLabelText('Espèce'), { target: { value: 'cat' } });
    await screen.findByRole('option', { name: 'Chat' });
    fireEvent.change(screen.getByLabelText('Espèce'), { target: { value: 'cat' } });
    fireEvent.click(screen.getByRole('button', { name: 'Créer' }));

    const dialog = await screen.findByRole('dialog');
    expect(
      within(dialog).getByRole('heading', { name: 'Enregistrer une admission' }),
    ).toBeInTheDocument();
  });
});

describe('latestInEffect', () => {
  it('skips voids and voided movements', () => {
    const voided = movement({ id: MOVE, type: 'relocation', voidedByMovementId: 'v1' });
    const voidItem = movement({ id: 'v1', type: 'void', voidsMovementId: MOVE });
    const intake = movement({});

    expect(latestInEffect([voidItem, voided, intake])).toBe(intake);
    expect(latestInEffect([])).toBeUndefined();
  });
});
