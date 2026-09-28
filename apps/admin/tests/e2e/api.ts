import type { Page } from '@playwright/test';

export const LOCALE_STORAGE_KEY = 'shelter.locale';

export const ORG_A = '11111111-1111-1111-1111-111111111111';
export const ORG_B = '22222222-2222-2222-2222-222222222222';

export const species = [
  { code: 'dog', label: { fr: 'Chien', en: 'Dog' } },
  { code: 'cat', label: { fr: 'Chat', en: 'Cat' } },
];

export interface SessionOptions {
  activeOrganizationId?: string | null;
  permissions?: string[];
  mfaEnrollmentRequired?: boolean;
  memberships?: { organizationId: string; organizationName: string }[];
}

export function sessionBody(options: SessionOptions = {}) {
  return {
    user: {
      id: 'u1',
      displayName: 'Camille Tremblay',
      preferredLanguage: 'fr',
      isPlatformOperator: false,
      mfaEnabled: !options.mfaEnrollmentRequired,
    },
    memberships: options.memberships ?? [{ organizationId: ORG_A, organizationName: 'Refuge A' }],
    activeOrganizationId:
      options.activeOrganizationId === undefined ? ORG_A : options.activeOrganizationId,
    permissions: options.permissions ?? ['animal.read', 'movement.read', 'platform.staff.read'],
    mfaEnrollmentRequired: options.mfaEnrollmentRequired ?? false,
  };
}

/**
 * No backend in E2E: every API call is mocked. Later routes win in Playwright, so the catch-all goes
 * first. The antiforgery endpoint sets the readable cookie the fetcher echoes on unsafe requests.
 */
export async function mockBaseApi(page: Page) {
  await page.route('**/api/**', (route) => route.fulfill({ json: { status: 'ok' } }));
  await page.route('**/api/animals/species', (route) => route.fulfill({ json: species }));
  await page.route('**/api/platform/session/antiforgery', (route) =>
    route.fulfill({ status: 204, headers: { 'Set-Cookie': 'XSRF-TOKEN=e2e-token; Path=/' } }),
  );
}

/** Seeds the UI locale once per test (a reload keeps whatever the test switched to). */
export async function seedLocale(page: Page, locale: string) {
  await page.addInitScript(
    ([key, value]) => {
      if (!sessionStorage.getItem('e2e-seeded')) {
        localStorage.setItem(key, value);
        sessionStorage.setItem('e2e-seeded', '1');
      }
    },
    [LOCALE_STORAGE_KEY, locale] as const,
  );
}
