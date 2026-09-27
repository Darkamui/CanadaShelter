import { useTranslation } from 'react-i18next';
import { SpeciesList } from '../components/SpeciesList';

/** Animals module home. Until M3 it only shows the species reference list. */
export function AnimalsPage() {
  const { t } = useTranslation();

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl font-semibold">{t('nav.animals')}</h1>
      <SpeciesList />
    </section>
  );
}
