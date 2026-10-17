import { Button } from '@shelter/ui/components/button';
import { cn } from '@shelter/ui/lib/utils';
import { Languages } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Locale } from '../../lib/i18n';

/** Bilingual toggle (canada.ca pattern): shows the other language, named in that language. */
export function LanguageSwitcher({ className }: { className?: string }) {
  const { t, i18n } = useTranslation();
  const other: Locale = i18n.language === 'en-CA' ? 'fr-CA' : 'en-CA';

  return (
    <Button
      variant="ghost"
      size="sm"
      lang={other}
      className={cn('text-muted-foreground hover:text-foreground', className)}
      onClick={() => void i18n.changeLanguage(other)}
    >
      <Languages aria-hidden="true" />
      {t(`language.${other}`)}
    </Button>
  );
}
