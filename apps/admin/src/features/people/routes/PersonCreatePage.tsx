import { getListPeopleQueryKey, useCreatePerson } from '@shelter/api-client/hooks/people';
import { PageHeader } from '@shelter/ui/components/page-header';
import { toast } from '@shelter/ui/components/toaster';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { BackLink } from '../../../lib/page/BackLink';
import { PersonForm } from '../components/PersonForm';
import { emptyPerson, type PersonPageState } from '../model';

/** New person. Possible duplicates never block: the record lists them after creation. */
export function PersonCreatePage() {
  const { t, i18n } = useTranslation('people');
  const create = useCreatePerson();
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <PageHeader above={<BackLink>{t('detail.backToList')}</BackLink>} title={t('create.title')} />
      <PersonForm
        defaultValues={emptyPerson(i18n.language)}
        submitLabel={t('create.submit')}
        cancelTo=".."
        onSubmit={async (data) => {
          const created = await create.mutateAsync({ data });
          await queryClient.invalidateQueries({ queryKey: getListPeopleQueryKey() });
          toast.success(t('detail.created'));
          const state: PersonPageState = { duplicates: created.possibleDuplicates };
          await navigate(`../${created.person.id}`, { relative: 'path', state });
        }}
      />
    </div>
  );
}
