import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router';
import { isModuleKey } from '../modules';
import { NotFoundPage } from './NotFoundPage';

/** Placeholder until each module ships its own routes under src/features/<module>/routes. */
export function ModulePlaceholderPage() {
  const { t } = useTranslation();
  const { module } = useParams();

  if (!isModuleKey(module)) {
    return <NotFoundPage />;
  }

  return (
    <section className="flex flex-col gap-2">
      <h1 className="text-2xl font-semibold">{t(`nav.${module}`)}</h1>
      <p className="text-muted-foreground">{t('module.comingSoon')}</p>
    </section>
  );
}
