import {
  getGetPersonQueryKey,
  getListPeopleQueryKey,
  useGetPerson,
  useUpdatePerson,
} from '@shelter/api-client/hooks/people';
import type { PersonResponse } from '@shelter/api-client/model';
import { PageHeader } from '@shelter/ui/components/page-header';
import { toast } from '@shelter/ui/components/toaster';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useNavigate, useParams } from 'react-router';
import { statusOf } from '../../../lib/auth/session';
import { BackLink } from '../../../lib/page/BackLink';
import { PageError, RecordSkeleton } from '../../../lib/page/PageStates';
import { PersonForm } from '../components/PersonForm';
import { fromPerson } from '../model';

/** Edits a person's name, contact details, roles and notes. */
export function PersonEditPage() {
  const { t } = useTranslation('people');
  const { personId = '' } = useParams();
  const person = useGetPerson(personId);

  if (person.isPending) return <RecordSkeleton />;
  if (person.isError) {
    return (
      <PageError above={<BackLink to="/people">{t('detail.backToList')}</BackLink>}>
        {statusOf(person.error) === 404 ? t('detail.notFound') : t('detail.loadFailed')}
      </PageError>
    );
  }
  return <EditPerson person={person.data} />;
}

function EditPerson({ person }: { person: PersonResponse }) {
  const { t } = useTranslation('people');
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const update = useUpdatePerson();

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <PageHeader
        above={<BackLink>{person.displayName}</BackLink>}
        title={t('edit.title', { name: person.displayName })}
      />
      <PersonForm
        key={person.updatedAt}
        defaultValues={fromPerson(person)}
        submitLabel={t('detail.save')}
        cancelTo=".."
        onSubmit={async (data) => {
          const saved = await update.mutateAsync({ personId: person.id, data });
          queryClient.setQueryData(getGetPersonQueryKey(person.id), saved);
          await queryClient.invalidateQueries({ queryKey: getListPeopleQueryKey() });
          toast.success(t('detail.saved'));
          await navigate('..', { relative: 'path' });
        }}
      />
    </div>
  );
}
