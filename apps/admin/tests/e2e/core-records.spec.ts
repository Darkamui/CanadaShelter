import { expect, test } from '@playwright/test';
import { mockBaseApi, seedLocale, sessionBody } from './api';
import { mockCoreRecordsApi } from './fakeCoreRecords';

const permissions = [
  'animal.read',
  'animal.write',
  'movement.read',
  'movement.write',
  'location.read',
  'location.write',
  'person.read',
  'person.write',
];

const cases = [
  {
    locale: 'fr-CA',
    locations: { new: 'Nouvel emplacement', name: 'Nom', kind: 'Type', create: 'Créer' },
    addInside: 'Ajouter un emplacement dans Pavillon A',
    building: 'Bâtiment',
    kennel: 'Enclos',
    person: { first: 'Prénom', last: 'Nom de famille', email: 'Courriel', create: 'Créer' },
    personCreated: 'La personne a été créée.',
    animal: { name: 'Nom', species: 'Espèce', create: 'Créer', cat: 'Chat' },
    intake: {
      reason: "Motif d'admission",
      stray: 'Errant',
      to: 'Vers',
      submit: "Enregistrer l'admission",
      done: "L'admission a été enregistrée.",
    },
    move: {
      open: 'Déplacer',
      submit: 'Enregistrer le déplacement',
      done: 'Le déplacement a été enregistré.',
    },
    population: { here: 'Animaux présents', one: '1 animal' },
    outcome: {
      open: 'Enregistrer une sortie',
      type: 'Type de sortie',
      search: 'Rechercher par nom, courriel ou téléphone',
      results: 'Personnes trouvées',
      submit: 'Enregistrer la sortie',
      done: 'La sortie a été enregistrée.',
    },
    timeline: [
      'Sortie enregistrée',
      'Déplacement enregistré',
      'Admission enregistrée',
      'Animal inscrit',
    ],
  },
  {
    locale: 'en-CA',
    locations: { new: 'New location', name: 'Name', kind: 'Kind', create: 'Create' },
    addInside: 'Add a location inside Pavillon A',
    building: 'Building',
    kennel: 'Kennel',
    person: { first: 'First name', last: 'Last name', email: 'Email', create: 'Create' },
    personCreated: 'The person was created.',
    animal: { name: 'Name', species: 'Species', create: 'Create', cat: 'Cat' },
    intake: {
      reason: 'Intake reason',
      stray: 'Stray',
      to: 'To',
      submit: 'Record the intake',
      done: 'The intake was recorded.',
    },
    move: { open: 'Move', submit: 'Record the move', done: 'The move was recorded.' },
    population: { here: 'Animals here', one: '1 animal' },
    outcome: {
      open: 'Record an outcome',
      type: 'Outcome',
      search: 'Search by name, email or phone',
      results: 'Matching people',
      submit: 'Record the outcome',
      done: 'The outcome was recorded.',
    },
    timeline: ['Outcome recorded', 'Move recorded', 'Intake recorded', 'Animal registered'],
  },
] as const;

for (const c of cases) {
  test.describe(c.locale, () => {
    test.beforeEach(async ({ page }) => {
      await mockBaseApi(page);
      await page.route('**/api/platform/session', (route) =>
        route.fulfill({ json: sessionBody({ permissions }) }),
      );
      await mockCoreRecordsApi(page);
      await seedLocale(page, c.locale);
    });

    test('location → person → animal → intake → move → outcome → timeline', async ({ page }) => {
      // Locations: a building with two kennels.
      await page.goto('/operations');
      await page.getByRole('button', { name: c.locations.new }).click();
      let dialog = page.getByRole('dialog');
      await dialog.getByLabel(c.locations.name, { exact: true }).fill('Pavillon A');
      await dialog
        .getByLabel(c.locations.kind, { exact: true })
        .selectOption({ label: c.building });
      await dialog.getByRole('button', { name: c.locations.create }).click();
      await expect(dialog).toBeHidden();
      for (const kennel of ['Enclos 1', 'Enclos 2']) {
        await page.getByRole('button', { name: c.addInside }).click();
        dialog = page.getByRole('dialog');
        await dialog.getByLabel(c.locations.name, { exact: true }).fill(kennel);
        await dialog
          .getByLabel(c.locations.kind, { exact: true })
          .selectOption({ label: c.kennel });
        await dialog.getByRole('button', { name: c.locations.create }).click();
        await expect(dialog).toBeHidden();
        await expect(page.getByRole('link', { name: kennel })).toBeVisible();
      }

      // A person.
      await page.goto('/people/new');
      await page.getByLabel(c.person.first, { exact: true }).fill('Marie');
      await page.getByLabel(c.person.last, { exact: true }).fill('Tremblay');
      await page.getByLabel(c.person.email, { exact: true }).fill('marie@example.com');
      await page.getByRole('button', { name: c.person.create }).click();
      await expect(page.getByText(c.personCreated)).toBeVisible();

      // An animal, then its intake straight away.
      await page.goto('/animals/new');
      await page.getByLabel(c.animal.name, { exact: true }).fill('Éclair');
      await page
        .getByLabel(c.animal.species, { exact: true })
        .selectOption({ label: c.animal.cat });
      await page.getByRole('button', { name: c.animal.create, exact: true }).click();

      dialog = page.getByRole('dialog');
      await dialog.getByLabel(c.intake.reason).selectOption({ label: c.intake.stray });
      await dialog
        .getByLabel(c.intake.to, { exact: true })
        .selectOption({ label: 'Pavillon A › Enclos 1' });
      await dialog.getByRole('button', { name: c.intake.submit }).click();
      await expect(page.getByText(c.intake.done)).toBeVisible();

      // Move to the other kennel.
      await page.getByRole('button', { name: c.move.open, exact: true }).click();
      dialog = page.getByRole('dialog');
      await dialog
        .getByLabel(c.intake.to, { exact: true })
        .selectOption({ label: 'Pavillon A › Enclos 2' });
      await dialog.getByRole('button', { name: c.move.submit }).click();
      await expect(page.getByText(c.move.done)).toBeVisible();

      // The building's page lists the animal in its subtree.
      await page.goto('/operations');
      await page.getByRole('link', { name: 'Pavillon A' }).click();
      await expect(page.getByRole('heading', { level: 1, name: 'Pavillon A' })).toBeVisible();
      await expect(page.getByRole('listitem').filter({ hasText: 'Enclos 2' })).toContainText(
        c.population.one,
      );
      const here = page.getByRole('region', { name: c.population.here });
      await here.getByRole('link', { name: 'Éclair' }).click();

      // Adoption by the person.
      await page.getByRole('button', { name: c.outcome.open }).click();
      dialog = page.getByRole('dialog');
      await dialog.getByLabel(c.outcome.type).selectOption({ label: 'Adoption' });
      await dialog.getByLabel(c.outcome.search).fill('Marie');
      const results = dialog.getByLabel(c.outcome.results);
      await expect(results.getByRole('option', { name: /Marie Tremblay/ })).toBeAttached();
      await results.selectOption({ label: 'Marie Tremblay · marie@example.com' });
      await dialog.getByRole('button', { name: c.outcome.submit }).click();
      await expect(page.getByText(c.outcome.done)).toBeVisible();

      // The timeline tells the whole story, newest first.
      for (const entry of c.timeline) {
        await expect(page.getByText(entry, { exact: true })).toBeVisible();
      }
      await expect(page.getByText('Marie Tremblay').first()).toBeVisible();
    });
  });
}
