import { useGetAnimal } from '@shelter/api-client/hooks/animals';
import type { AnimalResponse } from '@shelter/api-client/model';
import { Avatar } from '@shelter/ui/components/avatar';
import { Button } from '@shelter/ui/components/button';
import { RecordHeader } from '@shelter/ui/components/record-header';
import { Pencil } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link, Outlet, useLocation, useParams } from 'react-router';
import { Permissions } from '../../../lib/auth/permissions';
import { hasPermission, statusOf, useSession } from '../../../lib/auth/session';
import { formatDate } from '../../../lib/format';
import { BackLink } from '../../../lib/page/BackLink';
import { PageError, RecordSkeleton } from '../../../lib/page/PageStates';
import { RecordTabs } from '../../../lib/page/RecordTabs';
import { CustodyActions } from '../../movements/components/CustodyActions';
import { AlertChips, CustodyBadge, SpeciesIcon } from '../components/AnimalBadges';
import { custodyTone, type AnimalPageState } from '../model';
import type { AnimalRecordContext } from '../useAnimalRecord';
import { useSpecies } from '../useSpecies';

/**
 * An animal's record: the header (status, alerts, custody actions, the facts staff check first) above its tabs. Each
 * tab is a nested route that reads the animal through `useAnimalRecord`.
 */
export function AnimalRecordLayout() {
  const { t } = useTranslation('animals');
  const { animalId = '' } = useParams();
  const animal = useGetAnimal(animalId);
  const back = <BackLink to="/animals">{t('detail.backToList')}</BackLink>;

  if (animal.isPending) return <RecordSkeleton />;
  if (animal.isError) {
    return (
      <PageError above={back}>
        {statusOf(animal.error) === 404 ? t('detail.notFound') : t('detail.loadFailed')}
      </PageError>
    );
  }

  const context: AnimalRecordContext = { animal: animal.data };
  return (
    <div className="flex flex-col gap-6">
      {back}
      <AnimalHeader animal={animal.data} />
      <Outlet context={context} />
    </div>
  );
}

function AnimalHeader({ animal }: { animal: AnimalResponse }) {
  const { t, i18n } = useTranslation('animals');
  const { data: session } = useSession();
  const species = useSpecies();
  const state = (useLocation().state ?? {}) as AnimalPageState;
  const canWrite = hasPermission(session, Permissions.animalWrite);
  const canReadMovements = hasPermission(session, Permissions.movementRead);
  const canReadLocations = hasPermission(session, Permissions.locationRead);
  const activeIdentifiers = animal.identifiers.filter((i) => i.isActive).length;

  const location =
    animal.currentLocationId && animal.currentLocationName ? (
      canReadLocations ? (
        <Link
          to={`/locations/${animal.currentLocationId}`}
          className="underline-offset-4 hover:underline"
        >
          {animal.currentLocationName}
        </Link>
      ) : (
        animal.currentLocationName
      )
    ) : (
      <span className="font-normal text-muted-foreground">{t('detail.noLocation')}</span>
    );

  return (
    <RecordHeader
      tone={custodyTone[animal.custodyStatus] ?? 'neutral'}
      avatar={
        <Avatar size="lg">
          <SpeciesIcon code={animal.speciesCode} />
        </Avatar>
      }
      title={
        <>
          <span className="tabular text-muted-foreground">#{animal.number}</span>
          {animal.name && <> {animal.name}</>}
        </>
      }
      status={<CustodyBadge status={animal.custodyStatus} />}
      chips={<AlertChips animal={animal} />}
      actions={
        <>
          {canWrite && (
            <Button asChild variant="outline">
              <Link to="edit">
                <Pencil aria-hidden="true" />
                {t('detail.edit')}
              </Link>
            </Button>
          )}
          <CustodyActions animal={animal} startIntake={state.startIntake} />
        </>
      }
      facts={[
        { label: t('fields.location'), value: location },
        {
          label: t('detail.inCareSinceLabel'),
          value: animal.inCareSince ? (
            formatDate(animal.inCareSince, i18n.language)
          ) : (
            <span className="font-normal text-muted-foreground">—</span>
          ),
        },
        { label: t('fields.species'), value: species.labelOf(animal.speciesCode) },
        { label: t('fields.sex'), value: t(`sexes.${animal.sex}`, { defaultValue: animal.sex }) },
      ]}
      tabs={
        <RecordTabs
          tabs={[
            { to: '.', label: t('tabs.overview') },
            ...(canReadMovements ? [{ to: 'movements', label: t('tabs.movements') }] : []),
            { to: 'identifiers', label: t('tabs.identifiers'), count: activeIdentifiers },
            { to: 'history', label: t('tabs.history') },
          ]}
        />
      }
    />
  );
}
