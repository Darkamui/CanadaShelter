import { getListPeopleQueryKey, useCreatePerson } from '@shelter/api-client/hooks/people';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';
import { PersonForm } from '../components/PersonForm';
import { emptyPerson } from '../model';
import type { PersonPageState } from './PersonDetailPage';

/** New person. Possible duplicates never block: the detail page lists them after creation. */
export function PersonCreatePage() {
  const { t, i18n } = useTranslation('people');
  const create = useCreatePerson();
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  return (
    <section className="flex flex-col gap-4">
      <Link to=".." relative="path" className="text-sm underline-offset-4 hover:underline">
        {t('detail.backToList')}
      </Link>
      <h1 className="text-2xl font-semibold">{t('create.title')}</h1>
      <PersonForm
        defaultValues={emptyPerson(i18n.language)}
        submitLabel={t('create.submit')}
        onSubmit={async (data) => {
          const created = await create.mutateAsync({ data });
          await queryClient.invalidateQueries({ queryKey: getListPeopleQueryKey() });
          const state: PersonPageState = {
            created: true,
            duplicates: created.possibleDuplicates,
          };
          await navigate(`../${created.person.id}`, { relative: 'path', state });
        }}
      />
    </section>
  );
}
