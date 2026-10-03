import { getListAnimalsQueryKey, useCreateAnimal } from '@shelter/api-client/hooks/animals';
import { useQueryClient } from '@tanstack/react-query';
import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';
import { Permissions } from '../../../lib/auth/permissions';
import { hasPermission, useSession } from '../../../lib/auth/session';
import { AnimalForm } from '../components/AnimalForm';
import { emptyAnimal, toCreateRequest } from '../model';
import type { AnimalPageState } from './AnimalDetailPage';

/**
 * New animal: the server gives it the next number. It is not in care until an intake is recorded; with
 * `movement.write`, the detail page opens the intake dialog next unless the box is cleared.
 */
export function AnimalCreatePage() {
  const { t } = useTranslation('animals');
  const create = useCreateAnimal();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { data: session } = useSession();
  const canRecordIntake = hasPermission(session, Permissions.movementWrite);
  const [startIntake, setStartIntake] = useState(true);
  const startIntakeId = useId();

  return (
    <section className="flex flex-col gap-4">
      <Link to=".." relative="path" className="text-sm underline-offset-4 hover:underline">
        {t('detail.backToList')}
      </Link>
      <h1 className="text-2xl font-semibold">{t('create.title')}</h1>
      {canRecordIntake && (
        <div className="flex items-center gap-2">
          <input
            id={startIntakeId}
            type="checkbox"
            className="size-4"
            checked={startIntake}
            onChange={(e) => setStartIntake(e.target.checked)}
          />
          <label htmlFor={startIntakeId} className="text-sm">
            {t('create.startIntake')}
          </label>
        </div>
      )}
      <AnimalForm
        defaultValues={emptyAnimal()}
        submitLabel={t('create.submit')}
        withMicrochip
        onSubmit={async (values) => {
          const created = await create.mutateAsync({ data: toCreateRequest(values) });
          await queryClient.invalidateQueries({ queryKey: getListAnimalsQueryKey() });
          const state: AnimalPageState = {
            created: true,
            startIntake: canRecordIntake && startIntake,
          };
          await navigate(`../${created.id}`, { relative: 'path', state });
        }}
      />
    </section>
  );
}
