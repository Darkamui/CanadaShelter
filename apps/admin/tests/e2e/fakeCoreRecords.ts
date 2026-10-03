import type { Page, Route } from '@playwright/test';
import { species } from './api';

/**
 * A small in-memory stand-in for the M3 core-record endpoints (locations, people, animals, movements, timeline,
 * population), enough for the UI flow to round-trip. It applies the happy-path custody rules only; the real rules
 * are covered by the backend integration tests.
 */
export async function mockCoreRecordsApi(page: Page) {
  let clock = Date.parse('2026-10-01T12:00:00Z');
  const now = () => new Date((clock += 60_000)).toISOString();
  const id = () => crypto.randomUUID();

  const kinds = [
    { code: 'building', label: { fr: 'Bâtiment', en: 'Building' }, holdsAnimals: false },
    { code: 'kennel', label: { fr: 'Enclos', en: 'Kennel' }, holdsAnimals: true },
  ];
  const reasons = [{ code: 'stray', label: { fr: 'Errant', en: 'Stray' } }];
  const outcomes = [
    { code: 'adoption', label: { fr: 'Adoption', en: 'Adoption' }, requiresPerson: true },
  ];

  type Location = {
    id: string;
    parentId: string | null;
    kindCode: string;
    name: string;
    capacity: number | null;
    isArchived: boolean;
  };
  type Person = Record<string, unknown> & { id: string; displayName: string; email: string | null };
  type Animal = Record<string, unknown> & {
    id: string;
    number: number;
    custodyStatus: string;
    currentLocationId: string | null;
  };
  type Movement = Record<string, unknown> & { id: string; animalId: string };
  type TimelineEvent = Record<string, unknown> & { animalId: string };

  const locations: Location[] = [];
  const people: Person[] = [];
  const animals: Animal[] = [];
  const movements: Movement[] = [];
  const timeline: TimelineEvent[] = [];

  const nameOf = (locationId: string | null) =>
    locations.find((l) => l.id === locationId)?.name ?? null;
  const personName = (personId: string | null) =>
    people.find((p) => p.id === personId)?.displayName ?? null;
  const withLocation = (a: Animal) => ({ ...a, currentLocationName: nameOf(a.currentLocationId) });
  const page1 = <T>(items: T[], pageSize = 25) => ({
    items,
    page: 1,
    pageSize,
    totalCount: items.length,
  });
  const subtree = (rootId: string): Set<string> => {
    const ids = new Set([rootId]);
    for (let grew = true; grew;) {
      grew = false;
      for (const l of locations) {
        if (l.parentId && ids.has(l.parentId) && !ids.has(l.id)) {
          ids.add(l.id);
          grew = true;
        }
      }
    }
    return ids;
  };

  const record = (
    type: 'intake' | 'relocation' | 'outcome',
    body: Record<string, string | null>,
  ) => {
    const animal = animals.find((a) => a.id === body.animalId)!;
    const occurredAt = now();
    const from = animal.currentLocationId;
    const to = type === 'outcome' ? null : body.toLocationId;
    const movement: Movement = {
      id: id(),
      type,
      animalId: animal.id,
      reasonCode: body.reasonCode ?? body.outcomeCode ?? null,
      fromLocationId: from,
      fromLocationName: nameOf(from),
      toLocationId: to,
      toLocationName: nameOf(to),
      personId: body.personId ?? null,
      personName: personName(body.personId ?? null),
      notes: body.notes ?? null,
      occurredAt,
      recordedAt: occurredAt,
      voidsMovementId: null,
      voidedByMovementId: null,
    };
    movements.unshift(movement);
    Object.assign(animal, {
      custodyStatus: type === 'outcome' ? 'outcome' : 'in_care',
      currentLocationId: to,
      inCareSince: type === 'intake' ? occurredAt : animal.inCareSince,
      lastOutcomeCode: type === 'outcome' ? body.outcomeCode : animal.lastOutcomeCode,
    });
    const parameters: Record<string, string> = {};
    const names: Record<string, string> = {};
    if (body.reasonCode) parameters.reasonCode = body.reasonCode;
    if (body.outcomeCode) parameters.outcomeCode = body.outcomeCode;
    if (from && type !== 'intake') {
      parameters.fromLocationId = from;
      names.fromLocationId = nameOf(from)!;
    }
    if (to) {
      parameters.toLocationId = to;
      names.toLocationId = nameOf(to)!;
    }
    if (body.personId) {
      parameters.personId = body.personId;
      names.personId = personName(body.personId)!;
    }
    timeline.unshift({
      animalId: animal.id,
      id: id(),
      type: `${type}_recorded`,
      occurredAt,
      recordedAt: occurredAt,
      sourceModule: 'movements',
      sourceRecordId: movement.id,
      parameters,
      names,
    });
    return movement;
  };

  const handle = (route: Route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;
    const method = request.method();
    const body = () => request.postDataJSON() as Record<string, never>;
    const json = (data: unknown, status = 200) => route.fulfill({ status, json: data });
    let m: RegExpMatchArray | null;

    if (method === 'GET' && path === '/api/operations/location-kinds') return json(kinds);
    if (method === 'GET' && path === '/api/operations/locations') return json(locations);
    if (method === 'POST' && path === '/api/operations/locations') {
      const location: Location = { id: id(), isArchived: false, ...body() };
      locations.push(location);
      return json(location, 201);
    }

    if (method === 'GET' && path === '/api/people') {
      const q = (url.searchParams.get('q') ?? '').toLowerCase();
      return json(
        page1(
          people
            .filter((p) => p.displayName.toLowerCase().includes(q))
            .map((p) => ({
              id: p.id,
              displayName: p.displayName,
              email: p.email,
              phone: null,
              city: null,
              roleTags: [],
              isArchived: false,
            })),
        ),
      );
    }
    if (method === 'POST' && path === '/api/people') {
      const request = body();
      const at = now();
      const person: Person = {
        ...request,
        id: id(),
        displayName:
          request.displayName ?? [request.firstName, request.lastName].filter(Boolean).join(' '),
        email: request.email ?? null,
        preferredLanguage: request.preferredLanguage ?? 'fr-CA',
        roleTags: request.roleTags ?? [],
        isArchived: false,
        createdAt: at,
        updatedAt: at,
      };
      people.push(person);
      return json({ person, possibleDuplicates: [] }, 201);
    }
    if (method === 'GET' && (m = path.match(/^\/api\/people\/([^/]+)$/))) {
      return json(people.find((p) => p.id === m![1]));
    }

    if (method === 'GET' && path === '/api/animals/species') return json(species);
    if (method === 'GET' && path === '/api/animals/population') {
      return json(
        locations
          .map((l) => {
            const below = subtree(l.id);
            const at = (ids: Set<string>) =>
              animals.filter(
                (a) =>
                  a.custodyStatus === 'in_care' &&
                  a.currentLocationId &&
                  ids.has(a.currentLocationId),
              ).length;
            return { locationId: l.id, count: at(new Set([l.id])), subtreeCount: at(below) };
          })
          .filter((p) => p.subtreeCount > 0),
      );
    }
    if (method === 'GET' && path === '/api/animals') {
      const locationId = url.searchParams.get('locationId');
      const status = url.searchParams.get('status');
      const ids = locationId ? subtree(locationId) : null;
      return json(
        page1(
          animals
            .filter((a) => !status || a.custodyStatus === status)
            .filter((a) => !ids || (a.currentLocationId && ids.has(a.currentLocationId)))
            .map((a) => ({ ...withLocation(a), hasAlerts: false })),
        ),
      );
    }
    if (method === 'POST' && path === '/api/animals') {
      const at = now();
      // The microchip becomes an identifier on the real API; the fake does not keep identifiers.
      const fields: Record<string, unknown> = { ...body() };
      delete fields.microchip;
      const animal: Animal = {
        ...fields,
        id: id(),
        number: animals.length + 1,
        custodyStatus: 'not_in_care',
        currentLocationId: null,
        inCareSince: null,
        lastOutcomeCode: null,
        identifiers: [],
        createdAt: at,
        updatedAt: at,
        version: 1,
      };
      animals.push(animal);
      timeline.unshift({
        animalId: animal.id,
        id: id(),
        type: 'animal_registered',
        occurredAt: at,
        recordedAt: at,
        sourceModule: 'animals',
        sourceRecordId: animal.id,
        parameters: {},
        names: {},
      });
      return json(withLocation(animal), 201);
    }
    if (method === 'GET' && (m = path.match(/^\/api\/animals\/([^/]+)\/timeline$/))) {
      const animalId = m[1];
      return json(
        page1(
          timeline.filter((e) => e.animalId === animalId).map(({ animalId: _, ...e }) => e),
          20,
        ),
      );
    }
    if (method === 'GET' && (m = path.match(/^\/api\/animals\/([^/]+)$/))) {
      const animal = animals.find((a) => a.id === m![1]);
      return animal ? json(withLocation(animal)) : json({ status: 404 }, 404);
    }

    if (method === 'GET' && path === '/api/movements/intake-reasons') return json(reasons);
    if (method === 'GET' && path === '/api/movements/outcome-types') return json(outcomes);
    if (method === 'GET' && path === '/api/movements') {
      return json(movements.filter((mv) => mv.animalId === url.searchParams.get('animalId')));
    }
    if (
      method === 'POST' &&
      (m = path.match(/^\/api\/movements\/(intakes|relocations|outcomes)$/))
    ) {
      const type = ({ intakes: 'intake', relocations: 'relocation', outcomes: 'outcome' } as const)[
        m[1] as 'intakes' | 'relocations' | 'outcomes'
      ];
      return json(record(type, body()), 201);
    }

    return route.fallback();
  };

  for (const prefix of ['operations', 'people', 'animals', 'movements']) {
    await page.route(`**/api/${prefix}**`, handle);
  }
}
