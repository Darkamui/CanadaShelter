import {
  getGetPersonQueryKey,
  getListPeopleQueryKey,
  useArchivePerson,
  useGetPerson,
  useUnarchivePerson,
  useUpdatePerson,
} from '@shelter/api-client/hooks/people';
import type { PersonMatch, PersonResponse } from '@shelter/api-client/model';
import { Button } from '@shelter/ui/components/button';
import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useLocation, useParams } from 'react-router';
import { Permissions } from '../../../lib/auth/permissions';
import { hasPermission, statusOf, useSession } from '../../../lib/auth/session';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { PersonForm } from '../components/PersonForm';
import { RoleTags } from '../components/RoleTags';
import { fromPerson } from '../model';

/** Navigation state from the create page. */
export type PersonPageState = { created?: boolean; duplicates?: PersonMatch[] };

/** One person: an edit form with `person.write`, read-only details otherwise; archive and unarchive. */
export function PersonDetailPage() {
  const { t } = useTranslation('people');
  const { personId = '' } = useParams();
  const person = useGetPerson(personId);
  const state = (useLocation().state ?? {}) as PersonPageState;

  return (
    <section className="flex flex-col gap-4">
      <Link to=".." relative="path" className="text-sm underline-offset-4 hover:underline">
        {t('detail.backToList')}
      </Link>
      {person.isPending ? (
        <p role="status">{t('shell:list.loading')}</p>
      ) : person.isError ? (
        <FormAlert tone="error">
          {statusOf(person.error) === 404 ? t('detail.notFound') : t('detail.loadFailed')}
        </FormAlert>
      ) : (
        <PersonDetails person={person.data} state={state} />
      )}
    </section>
  );
}

function PersonDetails({ person, state }: { person: PersonResponse; state: PersonPageState }) {
  const { t } = useTranslation('people');
  const { data: session } = useSession();
  const canWrite = hasPermission(session, Permissions.personWrite);
  const queryClient = useQueryClient();
  const update = useUpdatePerson();
  const archive = useArchivePerson();
  const unarchive = useUnarchivePerson();
  const [message, setMessage] = useState<{ tone: 'error' | 'info'; text: string } | undefined>(
    state.created ? { tone: 'info', text: t('detail.created') } : undefined,
  );

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: getGetPersonQueryKey(person.id) }),
      queryClient.invalidateQueries({ queryKey: getListPeopleQueryKey() }),
    ]);
  const toggleArchive = () =>
    (person.isArchived ? unarchive : archive).mutate(
      { personId: person.id },
      {
        onSuccess: async () => {
          setMessage(undefined);
          await refresh();
        },
        onError: () => setMessage({ tone: 'error', text: t('form.unexpectedError') }),
      },
    );
  const duplicates = state.duplicates ?? [];

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-2xl font-semibold">{person.displayName}</h1>
        {canWrite && (
          <Button
            variant="outline"
            disabled={archive.isPending || unarchive.isPending}
            onClick={toggleArchive}
          >
            {person.isArchived ? t('detail.unarchive') : t('detail.archive')}
          </Button>
        )}
      </div>
      {person.isArchived && <FormAlert tone="info">{t('detail.archivedNotice')}</FormAlert>}
      {message && <FormAlert tone={message.tone}>{message.text}</FormAlert>}
      {duplicates.length > 0 && (
        <FormAlert tone="info">
          <p className="font-medium">{t('detail.duplicatesTitle')}</p>
          <p>{t('detail.duplicatesBody')}</p>
          <ul className="mt-1 list-disc pl-5">
            {duplicates.map((d) => (
              <li key={d.id}>
                <Link to={`../${d.id}`} relative="path" className="underline">
                  {d.displayName}
                </Link>
                {d.isArchived && ` (${t('list.archived')})`}
              </li>
            ))}
          </ul>
        </FormAlert>
      )}
      {canWrite ? (
        <PersonForm
          key={person.updatedAt}
          defaultValues={fromPerson(person)}
          submitLabel={t('detail.save')}
          onSubmit={async (data) => {
            const saved = await update.mutateAsync({ personId: person.id, data });
            queryClient.setQueryData(getGetPersonQueryKey(person.id), saved);
            await queryClient.invalidateQueries({ queryKey: getListPeopleQueryKey() });
            setMessage({ tone: 'info', text: t('detail.saved') });
          }}
        />
      ) : (
        <ReadOnlyPerson person={person} />
      )}
    </>
  );
}

function ReadOnlyPerson({ person }: { person: PersonResponse }) {
  const { t } = useTranslation('people');
  const address = [person.addressLine, person.city, person.province, person.postalCode]
    .filter(Boolean)
    .join(', ');
  const rows: [string, React.ReactNode][] = [
    [t('fields.email'), person.email],
    [t('fields.phone'), person.phone],
    [t('fields.secondaryPhone'), person.secondaryPhone],
    [t('fields.address'), address],
    [
      t('fields.preferredLanguage'),
      t(`languages.${person.preferredLanguage === 'en' ? 'en' : 'fr'}`),
    ],
    [t('fields.roleTags'), <RoleTags key="roles" roles={person.roleTags} />],
    [t('fields.notes'), person.notes],
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
