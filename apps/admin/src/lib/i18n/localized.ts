import { useCallback } from 'react';
import { useTranslation } from 'react-i18next';

/** A value the API returns in both languages (reference data, ADR 0013). */
export interface LocalizedValue {
  fr: string;
  en: string;
}

/** The text for `locale` (`en`, `en-CA`, …); French for anything else, as the backend resolves it. */
export function localize(text: LocalizedValue, locale: string | undefined): string {
  const language = locale?.toLowerCase();
  return language === 'en' || language?.startsWith('en-') ? text.en : text.fr;
}

/** `localize` bound to the current UI locale; re-renders on language change, no refetch needed. */
export function useLocalize(): (text: LocalizedValue) => string {
  const { i18n } = useTranslation();
  const locale = i18n.resolvedLanguage ?? i18n.language;
  return useCallback((text: LocalizedValue) => localize(text, locale), [locale]);
}
