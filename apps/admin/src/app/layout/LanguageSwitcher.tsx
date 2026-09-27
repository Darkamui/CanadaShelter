import { Button } from '@shelter/ui/components/button';
import { useTranslation } from 'react-i18next';
import type { Locale } from '../../lib/i18n';

/** Bilingual toggle (canada.ca pattern): shows the other language, named in that language. */
export function LanguageSwitcher() {
  const { t, i18n } = useTranslation();
  const other: Locale = i18n.language === 'en-CA' ? 'fr-CA' : 'en-CA';

  return (
    <Button
      variant="outline"
      size="sm"
      lang={other}
      onClick={() => void i18n.changeLanguage(other)}
    >
      {t(`language.${other}`)}
    </Button>
  );
}
