import { expect, test } from '@playwright/test';
import { mockBaseApi, seedLocale, sessionBody } from './api';

const cases = [
  {
    locale: 'fr-CA',
    other: 'en-CA',
    nav: 'Navigation principale',
    home: 'Accueil',
    animals: 'Animaux',
    switchTo: 'English',
    pingUp: 'Serveur connecté',
    dog: 'Chien',
  },
  {
    locale: 'en-CA',
    other: 'fr-CA',
    nav: 'Main navigation',
    home: 'Home',
    animals: 'Animals',
    switchTo: 'Français',
    pingUp: 'Server connected',
    dog: 'Dog',
  },
] as const;

for (const c of cases) {
  test.describe(c.locale, () => {
    test.beforeEach(async ({ page }) => {
      await mockBaseApi(page);
      await page.route('**/api/platform/session', (route) =>
        route.fulfill({ json: sessionBody() }),
      );
      await seedLocale(page, c.locale);
    });

    test('shell renders localized navigation for all modules', async ({ page }) => {
      await page.goto('/');

      await expect(page.locator('html')).toHaveAttribute('lang', c.locale);
      await expect(page.getByRole('heading', { level: 1, name: c.home })).toBeVisible();
      const nav = page.getByRole('navigation', { name: c.nav });
      await expect(nav.getByRole('link')).toHaveCount(9);
      await expect(page.getByRole('status')).toHaveText(c.pingUp);

      await nav.getByRole('link', { name: c.animals }).click();
      await expect(page).toHaveURL(/\/animals$/);
      await expect(page.getByRole('heading', { level: 1, name: c.animals })).toBeVisible();
      await expect(page.getByRole('listitem').filter({ hasText: c.dog })).toBeVisible();
    });

    test('language switch changes and persists the locale', async ({ page }) => {
      await page.goto('/');

      await page.getByRole('button', { name: c.switchTo }).click();
      await expect(page.locator('html')).toHaveAttribute('lang', c.other);

      await page.reload();
      await expect(page.locator('html')).toHaveAttribute('lang', c.other);
    });
  });
}
