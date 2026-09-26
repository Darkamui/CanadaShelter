import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

export const SUPPORTED_LOCALES = ['fr-CA', 'en-CA'] as const;
export type Locale = (typeof SUPPORTED_LOCALES)[number];
export const DEFAULT_LOCALE: Locale = 'fr-CA';

/** UI preference only (never auth data). */
export const LOCALE_STORAGE_KEY = 'shelter.locale';

type Messages = Record<string, unknown>;
type Resources = Record<string, Record<string, Messages>>;

// src/app/i18n/<locale>.json               → namespace "shell"
// src/features/<module>/i18n/<locale>.json → namespace "<module>"
const shellCatalogs = import.meta.glob<Messages>('../../app/i18n/*.json', {
  eager: true,
  import: 'default',
});
const featureCatalogs = import.meta.glob<Messages>('../../features/*/i18n/*.json', {
  eager: true,
  import: 'default',
});

export function buildResources(): Resources {
  const resources: Resources = {};
  const add = (locale: string, namespace: string, messages: Messages) => {
    (resources[locale] ??= {})[namespace] = messages;
  };

  for (const [path, messages] of Object.entries(shellCatalogs)) {
    const locale = /\/([^/]+)\.json$/.exec(path)?.[1];
    if (locale) add(locale, 'shell', messages);
  }
  for (const [path, messages] of Object.entries(featureCatalogs)) {
    const match = /features\/([^/]+)\/i18n\/([^/]+)\.json$/.exec(path);
    if (match?.[1] && match[2]) add(match[2], match[1], messages);
  }

  return resources;
}

export function isLocale(value: unknown): value is Locale {
  return SUPPORTED_LOCALES.includes(value as Locale);
}

function readStoredLocale(): Locale | undefined {
  try {
    const stored = localStorage.getItem(LOCALE_STORAGE_KEY);
    return isLocale(stored) ? stored : undefined;
  } catch {
    return undefined;
  }
}

function applyLocale(locale: string) {
  document.documentElement.lang = locale;
  document.title = i18n.t('shell:appName');
}

void i18n.use(initReactI18next).init({
  resources: buildResources(),
  lng: readStoredLocale() ?? DEFAULT_LOCALE,
  fallbackLng: DEFAULT_LOCALE,
  supportedLngs: SUPPORTED_LOCALES,
  load: 'currentOnly',
  defaultNS: 'shell',
  ns: ['shell'],
  interpolation: { escapeValue: false }, // React already escapes
  initAsync: false,
});

applyLocale(i18n.language);
i18n.on('languageChanged', (locale) => {
  applyLocale(locale);
  try {
    localStorage.setItem(LOCALE_STORAGE_KEY, locale);
  } catch {
    // Storage unavailable (private mode): the choice lasts for this page only.
  }
});

export default i18n;
