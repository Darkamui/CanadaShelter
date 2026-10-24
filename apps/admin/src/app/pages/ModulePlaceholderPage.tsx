import { EmptyState } from '@shelter/ui/components/empty-state';
import { PageHeader } from '@shelter/ui/components/page-header';
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

  const Icon = module.icon;
  return (
    <>
      <PageHeader title={t(`nav.${module.key}`)} description={t(`modules.${module.key}`)} />
      <EmptyState
        icon={<Icon />}
        title={t('module.comingSoon')}
        description={t('module.comingSoonBody')}
      />
    </>
  );
}
