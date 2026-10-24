import { getListAnimalsQueryKey, useCreateAnimal } from '@shelter/api-client/hooks/animals';
import { PageHeader } from '@shelter/ui/components/page-header';
import { toast } from '@shelter/ui/components/toaster';
import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { Permissions } from '../../../lib/auth/permissions';
import { hasPermission, useSession } from '../../../lib/auth/session';
import { CheckboxField } from '../../../lib/forms/FormField';
import { BackLink } from '../../../lib/page/BackLink';
import { AnimalForm } from '../components/AnimalForm';
import { emptyAnimal, toCreateRequest, type AnimalPageState } from '../model';

/**
 * New animal: the server gives it the next number. It is not in care until an intake is recorded; with
 * `movement.write`, the record opens the intake dialog next unless the box is cleared.
 */
export function AnimalCreatePage() {
  const { t } = useTranslation('animals');
  const create = useCreateAnimal();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { data: session } = useSession();
  const canRecordIntake = hasPermission(session, Permissions.movementWrite);
  const [startIntake, setStartIntake] = useState(true);

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <PageHeader above={<BackLink>{t('detail.backToList')}</BackLink>} title={t('create.title')} />
      <AnimalForm
        defaultValues={emptyAnimal()}
        submitLabel={t('create.submit')}
        cancelTo=".."
        withMicrochip
        extraActions={
          canRecordIntake && (
            <CheckboxField
              label={t('create.startIntake')}
              checked={startIntake}
              onChange={(e) => setStartIntake(e.target.checked)}
            />
          )
        }
        onSubmit={async (values) => {
          const created = await create.mutateAsync({ data: toCreateRequest(values) });
          await queryClient.invalidateQueries({ queryKey: getListAnimalsQueryKey() });
          toast.success(t('detail.created'));
          const state: AnimalPageState = { startIntake: canRecordIntake && startIntake };
          await navigate(`../${created.id}`, { relative: 'path', state });
        }}
      />
    </div>
  );
}
