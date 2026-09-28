import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router';
import { hasPermission, useSession } from '../../lib/auth/session';
import { MODULES } from '../modules';
import { ForbiddenPage } from './ForbiddenPage';
import { NotFoundPage } from './NotFoundPage';

/** Placeholder until each module ships its own routes under src/features/<module>/routes. */
export function ModulePlaceholderPage() {
  const { t } = useTranslation();
  const { module: key } = useParams();
  const { data: session } = useSession();
  const module = MODULES.find((m) => m.key === key);

  if (!module) {
    return <NotFoundPage />;
  }
  if (!hasPermission(session, module.permission)) {
    return <ForbiddenPage />;
  }

  return (
    <section className="flex flex-col gap-2">
      <h1 className="text-2xl font-semibold">{t(`nav.${module.key}`)}</h1>
      <p className="text-muted-foreground">{t('module.comingSoon')}</p>
    </section>
  );
}
