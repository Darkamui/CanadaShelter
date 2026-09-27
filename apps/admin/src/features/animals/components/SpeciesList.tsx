import { useListAnimalsSpecies } from '@shelter/api-client/hooks/animals';
import { useTranslation } from 'react-i18next';
import { useLocalize } from '../../../lib/i18n/localized';

/** Species reference list (GET /api/animals/species), labelled in the UI locale. */
export function SpeciesList() {
  const { t } = useTranslation('animals');
  const localize = useLocalize();
  const { data, isPending, isError } = useListAnimalsSpecies();

  return (
    <section aria-labelledby="species-title" className="flex flex-col gap-2">
      <h2 id="species-title" className="text-lg font-semibold">
        {t('species.title')}
      </h2>
      {isPending ? (
        <p className="text-muted-foreground">{t('species.loading')}</p>
      ) : isError ? (
        <p role="alert" className="text-destructive">
          {t('species.error')}
        </p>
      ) : (
        <ul className="list-inside list-disc">
          {data.map((species) => (
            <li key={species.code}>{localize(species.label)}</li>
          ))}
        </ul>
      )}
    </section>
  );
}
