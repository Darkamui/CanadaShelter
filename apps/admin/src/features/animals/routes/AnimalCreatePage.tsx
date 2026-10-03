import { getListAnimalsQueryKey, useCreateAnimal } from '@shelter/api-client/hooks/animals';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';
import { AnimalForm } from '../components/AnimalForm';
import { emptyAnimal, toCreateRequest } from '../model';
import type { AnimalPageState } from './AnimalDetailPage';

/** New animal: the server gives it the next number. It is not in care until an intake is recorded. */
export function AnimalCreatePage() {
  const { t } = useTranslation('animals');
  const create = useCreateAnimal();
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  return (
    <section className="flex flex-col gap-4">
      <Link to=".." relative="path" className="text-sm underline-offset-4 hover:underline">
        {t('detail.backToList')}
      </Link>
      <h1 className="text-2xl font-semibold">{t('create.title')}</h1>
      <AnimalForm
        defaultValues={emptyAnimal()}
        submitLabel={t('create.submit')}
        withMicrochip
        onSubmit={async (values) => {
          const created = await create.mutateAsync({ data: toCreateRequest(values) });
          await queryClient.invalidateQueries({ queryKey: getListAnimalsQueryKey() });
          const state: AnimalPageState = { created: true };
          await navigate(`../${created.id}`, { relative: 'path', state });
        }}
      />
    </section>
  );
}
