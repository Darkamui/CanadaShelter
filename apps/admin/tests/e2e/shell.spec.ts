import { expect, test } from '@playwright/test';

const LOCALE_STORAGE_KEY = 'shelter.locale';

const cases = [
  {
    locale: 'fr-CA',
    other: 'en-CA',
    nav: 'Navigation principale',
    home: 'Accueil',
    animals: 'Animaux',
    switchTo: 'English',
  },
  {
    locale: 'en-CA',
    other: 'fr-CA',
    nav: 'Main navigation',
    home: 'Home',
    animals: 'Animals',
    switchTo: 'Français',
  },
] as const;

for (const c of cases) {
  test.describe(c.locale, () => {
    test.beforeEach(async ({ page }) => {
      // No backend in E2E: every API call is mocked.
      await page.route('**/api/**', (route) => route.fulfill({ json: { status: 'ok' } }));
      await page.addInitScript(
        ([key, locale]) => {
          if (!sessionStorage.getItem('e2e-seeded')) {
            localStorage.setItem(key, locale);
            sessionStorage.setItem('e2e-seeded', '1');
          }
        },
        [LOCALE_STORAGE_KEY, c.locale] as const,
      );
    });

    test('shell renders localized navigation for all modules', async ({ page }) => {
      await page.goto('/');

      await expect(page.locator('html')).toHaveAttribute('lang', c.locale);
      await expect(page.getByRole('heading', { level: 1, name: c.home })).toBeVisible();
      const nav = page.getByRole('navigation', { name: c.nav });
      await expect(nav.getByRole('link')).toHaveCount(9);

      await nav.getByRole('link', { name: c.animals }).click();
      await expect(page).toHaveURL(/\/animals$/);
      await expect(page.getByRole('heading', { level: 1, name: c.animals })).toBeVisible();
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
