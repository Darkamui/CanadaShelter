import { expect, test } from '@playwright/test';
import { mockBaseApi, ORG_A, ORG_B, seedLocale, sessionBody, type SessionOptions } from './api';

const cases = [
  {
    locale: 'fr-CA',
    login: 'Connexion',
    email: 'Courriel',
    password: 'Mot de passe',
    submit: 'Se connecter',
    invalid: 'Courriel ou mot de passe incorrect.',
    organizations: 'Choisir un organisme',
    animals: 'Animaux',
    dog: 'Chien',
    enroll: 'Sécurisez votre compte',
    startMfa: 'Configurer l’application',
    setupKey: 'Clé de configuration',
    code: 'Code à six chiffres',
    confirm: 'Activer',
    savedCodes: 'J’ai conservé mes codes',
    logout: 'Se déconnecter',
    signedOut: 'Vous êtes maintenant déconnecté.',
  },
  {
    locale: 'en-CA',
    login: 'Sign in',
    email: 'Email',
    password: 'Password',
    submit: 'Sign in',
    invalid: 'Incorrect email or password.',
    organizations: 'Choose an organization',
    animals: 'Animals',
    dog: 'Dog',
    enroll: 'Secure your account',
    startMfa: 'Set up the app',
    setupKey: 'Setup key',
    code: 'Six-digit code',
    confirm: 'Turn on',
    savedCodes: 'I have saved my codes',
    logout: 'Sign out',
    signedOut: 'You are now signed out.',
  },
] as const;

const memberships = [
  { organizationId: ORG_A, organizationName: 'Refuge A' },
  { organizationId: ORG_B, organizationName: 'Refuge B' },
];

for (const c of cases) {
  test.describe(c.locale, () => {
    test.beforeEach(async ({ page }) => {
      await mockBaseApi(page);
      await seedLocale(page, c.locale);
    });

    test('sign in, choose an organization, land on the requested page, sign out', async ({
      page,
    }) => {
      // Server-side state of this fake session.
      let session: SessionOptions | null = null;
      const antiforgeryHeaders: (string | undefined)[] = [];

      await page.route('**/api/platform/session', (route) =>
        session
          ? route.fulfill({ json: sessionBody(session) })
          : route.fulfill({ status: 401, json: { status: 401 } }),
      );
      await page.route('**/api/platform/session/login', async (route) => {
        antiforgeryHeaders.push(route.request().headers()['x-xsrf-token']);
        const body = route.request().postDataJSON() as { password: string };
        if (body.password !== 'correct horse battery') {
          return route.fulfill({ status: 401, json: { status: 401 } });
        }
        session = { memberships, activeOrganizationId: null, permissions: [] };
        return route.fulfill({ json: { status: 'signedIn' } });
      });
      await page.route('**/api/platform/session/organization/*', (route) => {
        session = { memberships, activeOrganizationId: ORG_B };
        return route.fulfill({ status: 204 });
      });
      await page.route('**/api/platform/session/logout', (route) => {
        session = null;
        return route.fulfill({ status: 204 });
      });

      await page.goto('/animals');

      await expect(page).toHaveURL(/\/login$/);
      await expect(page.getByRole('heading', { level: 1, name: c.login })).toBeVisible();

      await page.getByLabel(c.email).fill('camille@example.org');
      await page.getByLabel(c.password).fill('wrong password!');
      await page.getByRole('button', { name: c.submit }).click();
      await expect(page.getByRole('alert')).toHaveText(c.invalid);

      await page.getByLabel(c.password).fill('correct horse battery');
      await page.getByRole('button', { name: c.submit }).click();

      await expect(page.getByRole('heading', { level: 1, name: c.organizations })).toBeVisible();
      await page.getByRole('button', { name: 'Refuge B' }).click();

      await expect(page).toHaveURL(/\/animals$/);
      await expect(page.getByRole('heading', { level: 1, name: c.animals })).toBeVisible();
      await expect(page.getByRole('cell', { name: c.dog })).toBeVisible();
      await expect(page.getByRole('banner')).toContainText('Refuge B');
      // Unsafe requests echo the antiforgery cookie.
      expect(antiforgeryHeaders).toEqual(['e2e-token', 'e2e-token']);

      await page.getByRole('button', { name: c.logout }).click();
      await expect(page).toHaveURL(/\/login$/);
      await expect(page.getByRole('status')).toHaveText(c.signedOut);
    });

    test('a role that requires MFA must enroll before using the app', async ({ page }) => {
      let enrolled = false;
      await page.route('**/api/platform/session', (route) =>
        route.fulfill({
          json: enrolled
            ? sessionBody()
            : sessionBody({ permissions: [], mfaEnrollmentRequired: true }),
        }),
      );
      await page.route('**/api/platform/session/mfa/setup', (route) =>
        route.fulfill({
          json: {
            sharedKey: 'JBSWY3DPEHPK3PXP',
            authenticatorUri: 'otpauth://totp/Shelter:camille?secret=JBSWY3DPEHPK3PXP',
          },
        }),
      );
      await page.route('**/api/platform/session/mfa/enable', (route) => {
        enrolled = true;
        return route.fulfill({ json: { recoveryCodes: ['aaaa-bbbb', 'cccc-dddd'] } });
      });

      await page.goto('/animals');

      await expect(page).toHaveURL(/\/mfa\/enroll$/);
      await expect(page.getByRole('heading', { level: 1, name: c.enroll })).toBeVisible();

      await page.getByRole('button', { name: c.startMfa }).click();
      await expect(page.getByLabel(c.setupKey)).toHaveText('JBSW Y3DP EHPK 3PXP');
      await page.getByLabel(c.code).fill('123456');
      await page.getByRole('button', { name: c.confirm }).click();

      await expect(page.getByRole('listitem').filter({ hasText: 'aaaa-bbbb' })).toBeVisible();
      await page.getByRole('button', { name: c.savedCodes }).click();

      await expect(page).toHaveURL(/\/animals$/);
      await expect(page.getByRole('heading', { level: 1, name: c.animals })).toBeVisible();
    });
  });
}
