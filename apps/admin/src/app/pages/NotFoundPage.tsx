import { Button } from '@shelter/ui/components/button';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

export function NotFoundPage() {
  const { t } = useTranslation();

  return (
    <section className="flex flex-col items-start gap-4">
      <h1 className="text-2xl font-semibold">{t('notFound.title')}</h1>
      <Button asChild variant="link" className="px-0">
        <Link to="/">{t('notFound.backHome')}</Link>
      </Button>
    </section>
  );
}
