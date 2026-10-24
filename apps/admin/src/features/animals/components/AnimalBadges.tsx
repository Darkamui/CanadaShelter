import { Badge } from '@shelter/ui/components/badge';
import { cn } from '@shelter/ui/lib/utils';
import { Bird, Cat, Dog, HeartPulse, PawPrint, Rabbit, Scale, ShieldAlert } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { ALERTS, custodyTone } from '../model';

const speciesIcons: Record<string, typeof PawPrint> = {
  dog: Dog,
  cat: Cat,
  rabbit: Rabbit,
  bird: Bird,
};

/** The species as a picture until animals have photos; a paw for species without their own icon. */
export function SpeciesIcon({ code, className }: { code: string; className?: string }) {
  const Icon = speciesIcons[code] ?? PawPrint;
  return <Icon aria-hidden="true" className={className} />;
}

const alertIcons = { medical: HeartPulse, behaviour: ShieldAlert, legal: Scale } as const;

/** Custody status as a badge in its tone. */
export function CustodyBadge({ status }: { status: string }) {
  const { t } = useTranslation('animals');
  return (
    <Badge tone={custodyTone[status] ?? 'neutral'} dot>
      {t(`custodyStatuses.${status}`, { defaultValue: status })}
    </Badge>
  );
}

/** One chip per alert the animal has (medical, behaviour, legal); the text itself is on the overview. */
export function AlertChips({
  animal,
}: {
  animal: Record<(typeof ALERTS)[number]['field'], string | null>;
}) {
  const { t } = useTranslation('animals');
  return ALERTS.filter(({ field }) => animal[field]).map(({ key, tone }) => {
    const Icon = alertIcons[key];
    return (
      <Badge key={key} tone={tone}>
        <Icon aria-hidden="true" />
        {t(`alerts.${key}`)}
      </Badge>
    );
  });
}

const noteClass = {
  danger: 'border-danger-border bg-danger-soft [&>svg]:text-danger',
  warning: 'border-warning-border bg-warning-soft [&>svg]:text-warning',
  info: 'border-info-border bg-info-soft [&>svg]:text-info',
} as const;

/** The text of each alert, in the alert's tone; nothing when the animal has none. */
export function AlertNotes({
  animal,
}: {
  animal: Record<(typeof ALERTS)[number]['field'], string | null>;
}) {
  const { t } = useTranslation('animals');
  const present = ALERTS.filter(({ field }) => animal[field]);
  if (present.length === 0) return null;

  return (
    <ul className="flex flex-col gap-2">
      {present.map(({ key, field, tone }) => {
        const Icon = alertIcons[key];
        return (
          <li key={key} className={cn('flex gap-3 rounded-lg border p-3', noteClass[tone])}>
            <Icon aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
            <div className="flex min-w-0 flex-col gap-0.5">
              <p className="text-sm font-medium">{t(`fields.${field}`)}</p>
              <p className="text-sm break-words whitespace-pre-line">{animal[field]}</p>
            </div>
          </li>
        );
      })}
    </ul>
  );
}
