import { Button } from '@shelter/ui/components/button';
import { Card, CardAction, CardContent, CardHeader, CardTitle } from '@shelter/ui/components/card';
import { DescriptionList } from '@shelter/ui/components/description-list';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { Permissions } from '../../../lib/auth/permissions';
import { hasPermission, useSession } from '../../../lib/auth/session';
import { formatDay } from '../../../lib/format';
import { MovementsPanel } from '../../movements/components/MovementsPanel';
import { useTimelineValueFormatter } from '../../movements/useMovementReference';
import { AlertNotes } from '../components/AnimalBadges';
import { IdentifiersPanel } from '../components/IdentifiersPanel';
import { RecentActivity, TimelinePanel } from '../components/TimelinePanel';
import { useAnimalRecord } from '../useAnimalRecord';

/** What staff need at the kennel door: alerts first, then the profile, identifiers and the latest events. */
export function AnimalOverviewTab() {
  const { t, i18n } = useTranslation('animals');
  const { animal } = useAnimalRecord();
  const formatValue = useTimelineValueFormatter();
  const active = animal.identifiers.filter((i) => i.isActive);
  const birthDate = animal.birthDate
    ? `${formatDay(animal.birthDate, i18n.language)}${animal.birthDateEstimated ? ` (${t('detail.estimated')})` : ''}`
    : null;
  const hasAlerts = Boolean(animal.medicalAlert || animal.behaviourAlert || animal.legalAlert);

  return (
    <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_20rem]">
      <div className="flex min-w-0 flex-col gap-6">
        {hasAlerts && (
          <Card aria-labelledby="alerts-title">
            <CardHeader>
              <CardTitle id="alerts-title">{t('alerts.title')}</CardTitle>
            </CardHeader>
            <CardContent>
              <AlertNotes animal={animal} />
            </CardContent>
          </Card>
        )}
        <Card aria-labelledby="profile-title">
          <CardHeader>
            <CardTitle id="profile-title">{t('overview.profile')}</CardTitle>
          </CardHeader>
          <CardContent>
            <DescriptionList
              items={[
                {
                  label: t('fields.breed'),
                  value: [animal.breed, animal.secondaryBreed].filter(Boolean).join(' / '),
                },
                { label: t('fields.colour'), value: animal.colour },
                {
                  label: t('fields.reproductiveStatus'),
                  value: t(`reproductiveStatuses.${animal.reproductiveStatus}`, {
                    defaultValue: animal.reproductiveStatus,
                  }),
                },
                { label: t('fields.birthDate'), value: birthDate },
                { label: t('fields.marks'), value: animal.marks, wide: true },
              ]}
            />
          </CardContent>
        </Card>
        <RecentActivity animalId={animal.id} formatValue={formatValue} />
      </div>
      <Card aria-labelledby="identifiers-summary-title">
        <CardHeader>
          <CardTitle id="identifiers-summary-title">{t('identifiers.title')}</CardTitle>
          <CardAction>
            <Button asChild variant="link" size="sm" className="h-auto px-0">
              <Link to="identifiers">{t('overview.manageIdentifiers')}</Link>
            </Button>
          </CardAction>
        </CardHeader>
        <CardContent>
          {active.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('identifiers.none')}</p>
          ) : (
            <dl className="flex flex-col gap-3">
              {active.map((identifier) => (
                <div key={identifier.id} className="flex flex-col gap-0.5">
                  <dt className="text-[0.8125rem] text-muted-foreground">
                    {t(`identifierTypes.${identifier.type}`, { defaultValue: identifier.type })}
                  </dt>
                  <dd className="tabular text-sm font-medium break-all">{identifier.value}</dd>
                </div>
              ))}
            </dl>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

export function AnimalMovementsTab() {
  const { animal } = useAnimalRecord();
  return <MovementsPanel animal={animal} />;
}

export function AnimalIdentifiersTab() {
  const { animal } = useAnimalRecord();
  const { data: session } = useSession();
  return (
    <IdentifiersPanel
      animalId={animal.id}
      identifiers={animal.identifiers}
      canWrite={hasPermission(session, Permissions.animalWrite)}
    />
  );
}

export function AnimalHistoryTab() {
  const { animal } = useAnimalRecord();
  const formatValue = useTimelineValueFormatter();
  return <TimelinePanel animalId={animal.id} formatValue={formatValue} />;
}
