import {
  getGetAnimalQueryKey,
  getListAnimalsQueryKey,
  useGetAnimal,
  useUpdateAnimal,
} from '@shelter/api-client/hooks/animals';
import type { AnimalResponse } from '@shelter/api-client/model';
import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useLocation, useParams } from 'react-router';
import { Permissions } from '../../../lib/auth/permissions';
import { hasPermission, statusOf, useSession } from '../../../lib/auth/session';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { formatDate, formatDay } from '../../../lib/format';
import { AnimalForm } from '../components/AnimalForm';
import { IdentifiersPanel } from '../components/IdentifiersPanel';
import { TimelinePanel } from '../components/TimelinePanel';
import { animalTitle, fromAnimal, toUpdateRequest } from '../model';
import { useSpecies } from '../useSpecies';

/** Navigation state from the create page. */
export type AnimalPageState = { created?: boolean };

/** One animal: custody summary, an edit form with `animal.write` (read-only details otherwise), identifiers, timeline. */
export function AnimalDetailPage() {
  const { t } = useTranslation('animals');
  const { animalId = '' } = useParams();
  const animal = useGetAnimal(animalId);
  const state = (useLocation().state ?? {}) as AnimalPageState;

  return (
    <section className="flex flex-col gap-6">
      <Link to=".." relative="path" className="text-sm underline-offset-4 hover:underline">
        {t('detail.backToList')}
      </Link>
      {animal.isPending ? (
        <p role="status">{t('shell:list.loading')}</p>
      ) : animal.isError ? (
        <FormAlert tone="error">
          {statusOf(animal.error) === 404 ? t('detail.notFound') : t('detail.loadFailed')}
        </FormAlert>
      ) : (
        <AnimalDetails animal={animal.data} state={state} />
      )}
    </section>
  );
}

function AnimalDetails({ animal, state }: { animal: AnimalResponse; state: AnimalPageState }) {
  const { t } = useTranslation('animals');
  const { data: session } = useSession();
  const canWrite = hasPermission(session, Permissions.animalWrite);
  const queryClient = useQueryClient();
  const update = useUpdateAnimal();
  const [message, setMessage] = useState<{ tone: 'error' | 'info'; text: string } | undefined>(
    state.created ? { tone: 'info', text: t('detail.created') } : undefined,
  );

  return (
    <>
      <h1 className="text-2xl font-semibold">{animalTitle(animal)}</h1>
      <CustodySummary animal={animal} />
      {message && <FormAlert tone={message.tone}>{message.text}</FormAlert>}
      {canWrite ? (
        <AnimalForm
          key={animal.version}
          defaultValues={fromAnimal(animal)}
          submitLabel={t('detail.save')}
          onSubmit={async (values) => {
            try {
              const saved = await update.mutateAsync({
                animalId: animal.id,
                data: toUpdateRequest(values, animal.version),
              });
              queryClient.setQueryData(getGetAnimalQueryKey(animal.id), saved);
              await queryClient.invalidateQueries({ queryKey: getListAnimalsQueryKey() });
              setMessage({ tone: 'info', text: t('detail.saved') });
            } catch (error) {
              if (statusOf(error) !== 409) throw error;
              // Someone else saved first: reload, which resets the form to their version.
              setMessage({ tone: 'error', text: t('detail.versionConflict') });
              await queryClient.invalidateQueries({ queryKey: getGetAnimalQueryKey(animal.id) });
            }
          }}
        />
      ) : (
        <ReadOnlyAnimal animal={animal} />
      )}
      <IdentifiersPanel animalId={animal.id} identifiers={animal.identifiers} canWrite={canWrite} />
      <TimelinePanel animalId={animal.id} />
    </>
  );
}

function CustodySummary({ animal }: { animal: AnimalResponse }) {
  const { t, i18n } = useTranslation('animals');
  const status = t(`custodyStatuses.${animal.custodyStatus}`, {
    defaultValue: animal.custodyStatus,
  });

  return (
    <FormAlert tone="info">
      <p>
        <span className="font-medium">{status}</span>
        {animal.currentLocationName && ` · ${animal.currentLocationName}`}
        {animal.inCareSince &&
          ` · ${t('detail.inCareSince', { date: formatDate(animal.inCareSince, i18n.language) })}`}
      </p>
    </FormAlert>
  );
}

function ReadOnlyAnimal({ animal }: { animal: AnimalResponse }) {
  const { t, i18n } = useTranslation('animals');
  const { labelOf } = useSpecies();
  const birthDate = animal.birthDate
    ? `${formatDay(animal.birthDate, i18n.language)}${animal.birthDateEstimated ? ` (${t('detail.estimated')})` : ''}`
    : null;
  const rows: [string, string | null][] = [
    [t('fields.name'), animal.name],
    [t('fields.species'), labelOf(animal.speciesCode)],
    [t('fields.breed'), [animal.breed, animal.secondaryBreed].filter(Boolean).join(' / ')],
    [t('fields.colour'), animal.colour],
    [t('fields.sex'), t(`sexes.${animal.sex}`, { defaultValue: animal.sex })],
    [
      t('fields.reproductiveStatus'),
      t(`reproductiveStatuses.${animal.reproductiveStatus}`, {
        defaultValue: animal.reproductiveStatus,
      }),
    ],
    [t('fields.birthDate'), birthDate],
    [t('fields.marks'), animal.marks],
    [t('fields.behaviourAlert'), animal.behaviourAlert],
    [t('fields.medicalAlert'), animal.medicalAlert],
    [t('fields.legalAlert'), animal.legalAlert],
  ];

  return (
    <dl className="grid max-w-2xl gap-x-6 gap-y-2 sm:grid-cols-[auto_1fr]">
      {rows.map(([label, value]) => (
        <div key={label} className="contents">
          <dt className="text-sm text-muted-foreground">{label}</dt>
          <dd className="whitespace-pre-line">{value || '—'}</dd>
        </div>
      ))}
    </dl>
  );
}
