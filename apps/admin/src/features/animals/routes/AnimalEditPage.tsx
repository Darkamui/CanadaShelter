import {
  getGetAnimalQueryKey,
  getListAnimalsQueryKey,
  useGetAnimal,
  useUpdateAnimal,
} from '@shelter/api-client/hooks/animals';
import type { AnimalResponse } from '@shelter/api-client/model';
import { PageHeader } from '@shelter/ui/components/page-header';
import { toast } from '@shelter/ui/components/toaster';
import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useParams } from 'react-router';
import { statusOf } from '../../../lib/auth/session';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { BackLink } from '../../../lib/page/BackLink';
import { PageError, RecordSkeleton } from '../../../lib/page/PageStates';
import { AnimalForm } from '../components/AnimalForm';
import { animalTitle, fromAnimal, toUpdateRequest } from '../model';

/** Edits an animal's description and alerts. A save that loses a race shows the other version to redo the changes. */
export function AnimalEditPage() {
  const { t } = useTranslation('animals');
  const { animalId = '' } = useParams();
  const animal = useGetAnimal(animalId);

  if (animal.isPending) return <RecordSkeleton />;
  if (animal.isError) {
    return (
      <PageError above={<BackLink to="/animals">{t('detail.backToList')}</BackLink>}>
        {statusOf(animal.error) === 404 ? t('detail.notFound') : t('detail.loadFailed')}
      </PageError>
    );
  }
  return <EditAnimal animal={animal.data} />;
}

function EditAnimal({ animal }: { animal: AnimalResponse }) {
  const { t } = useTranslation('animals');
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const update = useUpdateAnimal();
  const [conflict, setConflict] = useState(false);
  const title = animalTitle(animal);

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <PageHeader above={<BackLink>{title}</BackLink>} title={t('edit.title', { name: title })} />
      <AnimalForm
        key={animal.version}
        defaultValues={fromAnimal(animal)}
        submitLabel={t('detail.save')}
        cancelTo=".."
        alert={conflict && <FormAlert tone="error">{t('detail.versionConflict')}</FormAlert>}
        onSubmit={async (values) => {
          try {
            const saved = await update.mutateAsync({
              animalId: animal.id,
              data: toUpdateRequest(values, animal.version),
            });
            queryClient.setQueryData(getGetAnimalQueryKey(animal.id), saved);
            await queryClient.invalidateQueries({ queryKey: getListAnimalsQueryKey() });
            toast.success(t('detail.saved'));
            await navigate('..', { relative: 'path' });
          } catch (error) {
            if (statusOf(error) !== 409) throw error;
            // Someone else saved first: reload, which resets the form to their version.
            setConflict(true);
            await queryClient.invalidateQueries({ queryKey: getGetAnimalQueryKey(animal.id) });
          }
        }}
      />
    </div>
  );
}
