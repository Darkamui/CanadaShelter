import { useTranslation } from 'react-i18next';
import { LanguageSwitcher } from './LanguageSwitcher';

export function Header() {
  const { t } = useTranslation();

  return (
    <header className="flex h-14 items-center justify-between border-b px-6">
      <span className="font-semibold">{t('appName')}</span>
      <div className="flex items-center gap-3">
        <LanguageSwitcher />
      </div>
    </header>
  );
}
