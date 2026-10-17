import { Button } from '@shelter/ui/components/button';
import { EmptyState } from '@shelter/ui/components/empty-state';
import { Lock } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

export function ForbiddenPage() {
  const { t } = useTranslation();

  return (
    <section className="flex flex-col gap-6">
      <h1 className="font-display text-[1.75rem] leading-tight font-semibold tracking-[-0.015em]">
        {t('forbidden.title')}
      </h1>
      <EmptyState
        icon={<Lock />}
        title={t('forbidden.body')}
        action={
          <Button asChild variant="outline">
            <Link to="/">{t('notFound.backHome')}</Link>
          </Button>
        }
      />
    </section>
  );
}
