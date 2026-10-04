import {
  getGetPersonQueryKey,
  getListPeopleQueryKey,
  useArchivePerson,
  useGetPerson,
  useUnarchivePerson,
} from '@shelter/api-client/hooks/people';
import type { PersonResponse } from '@shelter/api-client/model';
import { Alert } from '@shelter/ui/components/alert';
import { Avatar, initialsOf } from '@shelter/ui/components/avatar';
import { Badge } from '@shelter/ui/components/badge';
import { Button } from '@shelter/ui/components/button';
import { Card, CardContent, CardHeader, CardTitle } from '@shelter/ui/components/card';
import { DescriptionList } from '@shelter/ui/components/description-list';
import { RecordHeader } from '@shelter/ui/components/record-header';
import { toast } from '@shelter/ui/components/toaster';
import { useQueryClient } from '@tanstack/react-query';
import { Archive, ArchiveRestore, Pencil } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link, useLocation, useParams } from 'react-router';
import { Permissions } from '../../../lib/auth/permissions';
import { hasPermission, statusOf, useSession } from '../../../lib/auth/session';
import { BackLink } from '../../../lib/page/BackLink';
import { PageError, RecordSkeleton } from '../../../lib/page/PageStates';
import { RoleBadges } from '../components/RoleTags';
import { isPersonRole, PROVINCES, type PersonPageState } from '../model';

/** One person: who they are and how to reach them, with edit and archive for `person.write`. */
export function PersonDetailPage() {
  const { t } = useTranslation('people');
  const { personId = '' } = useParams();
  const person = useGetPerson(personId);
  const back = <BackLink to="/people">{t('detail.backToList')}</BackLink>;

  if (person.isPending) return <RecordSkeleton />;
  if (person.isError) {
    return (
      <PageError above={back}>
        {statusOf(person.error) === 404 ? t('detail.notFound') : t('detail.loadFailed')}
      </PageError>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      {back}
      <PersonHeader person={person.data} />
      <PersonBody person={person.data} />
    </div>
  );
}

function PersonHeader({ person }: { person: PersonResponse }) {
  const { t } = useTranslation('people');
  const { data: session } = useSession();
  const canWrite = hasPermission(session, Permissions.personWrite);
  const queryClient = useQueryClient();
  const archive = useArchivePerson();
  const unarchive = useUnarchivePerson();
  const known = person.roleTags.filter(isPersonRole);

  const toggleArchive = () =>
    (person.isArchived ? unarchive : archive).mutate(
      { personId: person.id },
      {
        onSuccess: async () => {
          toast.success(
            person.isArchived ? t('detail.unarchivedToast') : t('detail.archivedToast'),
          );
          await Promise.all([
            queryClient.invalidateQueries({ queryKey: getGetPersonQueryKey(person.id) }),
            queryClient.invalidateQueries({ queryKey: getListPeopleQueryKey() }),
          ]);
        },
        onError: () => toast.error(t('form.unexpectedError')),
      },
    );

  const none = <span className="font-normal text-muted-foreground">—</span>;

  return (
    <RecordHeader
      tone={person.isArchived ? 'neutral' : 'primary'}
      avatar={<Avatar size="lg">{initialsOf(person.displayName)}</Avatar>}
      title={person.displayName}
      status={person.isArchived && <Badge>{t('detail.archivedBadge')}</Badge>}
      chips={known.length > 0 && <RoleBadges roles={known} />}
      actions={
        canWrite && (
          <>
            <Button asChild variant="outline">
              <Link to="edit">
                <Pencil aria-hidden="true" />
                {t('detail.edit')}
              </Link>
            </Button>
            <Button
              variant="outline"
              disabled={archive.isPending || unarchive.isPending}
              onClick={toggleArchive}
            >
              {person.isArchived ? (
                <ArchiveRestore aria-hidden="true" />
              ) : (
                <Archive aria-hidden="true" />
              )}
              {person.isArchived ? t('detail.unarchive') : t('detail.archive')}
            </Button>
          </>
        )
      }
      facts={[
        {
          label: t('fields.email'),
          value: person.email ? (
            <a href={`mailto:${person.email}`} className="underline-offset-4 hover:underline">
              {person.email}
            </a>
          ) : (
            none
          ),
        },
        {
          label: t('fields.phone'),
          value: person.phone ? (
            <a href={`tel:${person.phone}`} className="underline-offset-4 hover:underline">
              {person.phone}
            </a>
          ) : (
            none
          ),
        },
        {
          label: t('fields.preferredLanguage'),
          value: t(`languages.${person.preferredLanguage === 'en' ? 'en' : 'fr'}`),
        },
      ]}
    />
  );
}

function PersonBody({ person }: { person: PersonResponse }) {
  const { t } = useTranslation('people');
  const duplicates = ((useLocation().state ?? {}) as PersonPageState).duplicates ?? [];
  const province = PROVINCES.find((p) => p === person.province);
  const address = [
    person.addressLine,
    [person.city, province ? t(`provinces.${province}`) : person.province]
      .filter(Boolean)
      .join(', '),
    person.postalCode,
  ]
    .filter(Boolean)
    .join('\n');

  return (
    <>
      {duplicates.length > 0 && (
        <Alert tone="warning" title={t('detail.duplicatesTitle')}>
          <p>{t('detail.duplicatesBody')}</p>
          <ul className="mt-1.5 flex flex-col gap-1">
            {duplicates.map((d) => (
              <li key={d.id}>
                <Link
                  to={`/people/${d.id}`}
                  className="font-medium text-foreground underline underline-offset-4"
                >
                  {d.displayName}
                </Link>
                {d.isArchived && (
                  <Badge className="ml-2 align-middle">{t('detail.archivedBadge')}</Badge>
                )}
              </li>
            ))}
          </ul>
        </Alert>
      )}
      {person.isArchived && <Alert tone="info">{t('detail.archivedNotice')}</Alert>}
      <div className="grid items-start gap-6 lg:grid-cols-2">
        <Card aria-labelledby="contact-title">
          <CardHeader>
            <CardTitle id="contact-title">{t('detail.contact')}</CardTitle>
          </CardHeader>
          <CardContent>
            <DescriptionList
              columns={1}
              items={[
                { label: t('fields.secondaryPhone'), value: person.secondaryPhone },
                { label: t('fields.address'), value: address },
              ]}
            />
          </CardContent>
        </Card>
        <Card aria-labelledby="notes-title">
          <CardHeader>
            <CardTitle id="notes-title">{t('detail.notes')}</CardTitle>
          </CardHeader>
          <CardContent>
            {person.notes ? (
              <p className="text-sm break-words whitespace-pre-line">{person.notes}</p>
            ) : (
              <p className="text-sm text-muted-foreground">{t('detail.noNotes')}</p>
            )}
          </CardContent>
        </Card>
      </div>
    </>
  );
}
