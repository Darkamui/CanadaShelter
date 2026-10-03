import type { LocationItem, LocationKindItem, SessionResponse } from '@shelter/api-client/model';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AppProviders } from '../../app/providers';
import { routes } from '../../app/router';
import i18n from '../../lib/i18n';
import { buildTree, flatten, subtreeIds } from './model';

const ORG = '11111111-1111-1111-1111-111111111111';
const BUILDING = '22222222-2222-2222-2222-222222222222';
const ROOM = '33333333-3333-3333-3333-333333333333';
const KENNEL = '44444444-4444-4444-4444-444444444444';

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

const kinds: LocationKindItem[] = [
  { code: 'building', label: { fr: 'Bâtiment', en: 'Building' }, holdsAnimals: false },
  { code: 'room', label: { fr: 'Salle', en: 'Room' }, holdsAnimals: true },
  { code: 'kennel', label: { fr: 'Enclos', en: 'Kennel' }, holdsAnimals: true },
];

const locations: LocationItem[] = [
  {
    id: KENNEL,
    parentId: ROOM,
    kindCode: 'kennel',
    name: 'Enclos 1',
    capacity: 1,
    isArchived: false,
  },
  {
    id: BUILDING,
    parentId: null,
    kindCode: 'building',
    name: 'Pavillon A',
    capacity: null,
    isArchived: false,
  },
  {
    id: ROOM,
    parentId: BUILDING,
    kindCode: 'room',
    name: 'Salle des chats',
    capacity: 12,
    isArchived: false,
  },
];

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

describe('location tree model', () => {
  it('nests by parent and never offers a location or its descendants as its own parent', () => {
    const tree = buildTree(locations);

    expect(tree.map((n) => n.location.name)).toEqual(['Pavillon A']);
    expect(flatten(tree).map((n) => [n.location.name, n.depth])).toEqual([
      ['Pavillon A', 0],
      ['Salle des chats', 1],
      ['Enclos 1', 2],
    ]);
    expect(subtreeIds(tree[0]!.children[0]!)).toEqual(new Set([ROOM, KENNEL]));
  });

  it('shows a location whose parent is not listed at the top level', () => {
    expect(buildTree([locations[0]!]).map((n) => n.location.id)).toEqual([KENNEL]);
  });
});

describe('locations page', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('fr-CA');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows the tree read-only without location.write', async () => {
    mockApi({
      'GET /api/platform/session': () => Response.json(sessionWith(['location.read'])),
      'GET /api/operations/locations': () => Response.json(locations),
      'GET /api/operations/location-kinds': () => Response.json(kinds),
    });
    renderAt('/operations');

    const tree = await screen.findByRole('list', { name: 'Arborescence des emplacements' });
    expect(within(tree).getByText('Salle des chats')).toBeInTheDocument();
    expect(within(tree).getByText('Capacité : 12')).toBeInTheDocument();
    expect(within(tree).getAllByText('Salle').length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: 'Nouvel emplacement' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Modifier Pavillon A' })).not.toBeInTheDocument();
  });

  it('adds a location inside another and sends the parent', async () => {
    let sent: unknown;
    mockApi({
      'GET /api/platform/session': () =>
        Response.json(sessionWith(['location.read', 'location.write'])),
      'GET /api/operations/locations': () => Response.json(locations),
      'GET /api/operations/location-kinds': () => Response.json(kinds),
      'POST /api/operations/locations': (init) => {
        sent = JSON.parse(String(init?.body));
        return Response.json(
          {
            id: 'new',
            parentId: ROOM,
            kindCode: 'kennel',
            name: 'Enclos 2',
            capacity: 2,
            isArchived: false,
          },
          { status: 201 },
        );
      },
    });
    renderAt('/operations');

    fireEvent.click(
      await screen.findByRole('button', { name: 'Ajouter un emplacement dans Salle des chats' }),
    );
    const dialog = await screen.findByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Créer' }));
    expect(await within(dialog).findAllByText('Ce champ est obligatoire.')).toHaveLength(2);

    fireEvent.change(within(dialog).getByLabelText('Nom'), { target: { value: 'Enclos 2' } });
    fireEvent.change(within(dialog).getByLabelText('Type'), { target: { value: 'kennel' } });
    fireEvent.change(within(dialog).getByLabelText('Capacité'), { target: { value: '2' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Créer' }));

    expect(await screen.findByText('« Enclos 2 » a été enregistré.')).toBeInTheDocument();
    expect(sent).toEqual({ name: 'Enclos 2', kindCode: 'kennel', parentId: ROOM, capacity: 2 });
  });

  it('maps a duplicate name onto the field', async () => {
    mockApi({
      'GET /api/platform/session': () =>
        Response.json(sessionWith(['location.read', 'location.write'])),
      'GET /api/operations/locations': () => Response.json(locations),
      'GET /api/operations/location-kinds': () => Response.json(kinds),
      [`PUT /api/operations/locations/${ROOM}`]: () =>
        Response.json(
          {
            status: 400,
            errors: { name: ['Another active location here already has this name.'] },
          },
          { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
        ),
    });
    renderAt('/operations');

    fireEvent.click(await screen.findByRole('button', { name: 'Modifier Salle des chats' }));
    const dialog = await screen.findByRole('dialog');
    // The room cannot be moved inside itself or its kennel.
    const parentOptions = within(within(dialog).getByLabelText('Situé dans')).getAllByRole(
      'option',
    );
    expect(parentOptions.map((o) => o.textContent?.trim())).toEqual([
      'Aucun (niveau supérieur)',
      'Pavillon A',
    ]);
    fireEvent.click(within(dialog).getByRole('button', { name: 'Enregistrer' }));
    expect(
      await within(dialog).findByText(
        'Un autre emplacement actif à cet endroit porte déjà ce nom.',
      ),
    ).toBeInTheDocument();
  });

  it('explains why a location with children cannot be archived', async () => {
    mockApi({
      'GET /api/platform/session': () =>
        Response.json(sessionWith(['location.read', 'location.write'])),
      'GET /api/operations/locations': () => Response.json(locations),
      'GET /api/operations/location-kinds': () => Response.json(kinds),
      [`POST /api/operations/locations/${BUILDING}/archive`]: () =>
        Response.json(
          { status: 409, code: 'location.hasActiveChildren' },
          { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
        ),
    });
    renderAt('/operations');

    fireEvent.click(await screen.findByRole('button', { name: 'Archiver Pavillon A' }));
    expect(
      await screen.findByText("Archivez ou déplacez d'abord les emplacements qu'il contient."),
    ).toBeInTheDocument();
  });
});
