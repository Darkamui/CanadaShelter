import { useListLocationKinds, useListLocations } from '@shelter/api-client/hooks/operations';
import {
  useListMovementsIntakeReasons,
  useListMovementsOutcomeTypes,
} from '@shelter/api-client/hooks/movements';
import { useCallback, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocalize } from '../../lib/i18n/localized';
import { buildTree, flatten } from '../locations/model';

/**
 * The organization's visible intake reasons, labelled in the UI locale. `labelOf` falls back to the code for a
 * reason since hidden, so past movements still show something.
 */
export function useIntakeReasons() {
  const localize = useLocalize();
  const reasons = useListMovementsIntakeReasons();
  const options = useMemo(
    () => (reasons.data ?? []).map((r) => ({ code: r.code, label: localize(r.label) })),
    [reasons.data, localize],
  );
  const labelOf = useCallback(
    (code: string) => options.find((r) => r.code === code)?.label ?? code,
    [options],
  );
  return { options, labelOf, status: reasons.status };
}

/** The organization's visible outcome types; `requiresPerson` for adoption and return to owner. */
export function useOutcomeTypes() {
  const localize = useLocalize();
  const outcomes = useListMovementsOutcomeTypes();
  const options = useMemo(
    () =>
      (outcomes.data ?? []).map((o) => ({
        code: o.code,
        label: localize(o.label),
        requiresPerson: o.requiresPerson,
      })),
    [outcomes.data, localize],
  );
  const labelOf = useCallback(
    (code: string) => options.find((o) => o.code === code)?.label ?? code,
    [options],
  );
  return { options, labelOf, status: outcomes.status };
}

/**
 * Active locations that hold animals, depth-first, each labelled with its path (`Pavillon A › Salle des chats`) so
 * kennels with the same name stay distinct.
 */
export function useHoldingLocations() {
  const locations = useListLocations({ includeArchived: false });
  const kinds = useListLocationKinds();

  const options = useMemo(() => {
    const holding = new Set((kinds.data ?? []).filter((k) => k.holdsAnimals).map((k) => k.code));
    const names = new Map<string, string>();
    const result: { id: string; label: string }[] = [];
    for (const { location } of flatten(buildTree(locations.data ?? []))) {
      const parent = location.parentId === null ? undefined : names.get(location.parentId);
      const label = parent ? `${parent} › ${location.name}` : location.name;
      names.set(location.id, label);
      if (holding.has(location.kindCode)) result.push({ id: location.id, label });
    }
    return result;
  }, [locations.data, kinds.data]);

  const status =
    locations.status === 'error' || kinds.status === 'error'
      ? 'error'
      : locations.status === 'success' && kinds.status === 'success'
        ? 'success'
        : 'pending';
  return { options, status };
}

/**
 * Labels the code parameters of movement timeline events: intake reason, outcome type and movement type. Hides the
 * voided movement's ID (the movement history shows which one); undefined for any other parameter.
 */
export function useTimelineValueFormatter() {
  const { t } = useTranslation('movements');
  const { labelOf: reasonLabel } = useIntakeReasons();
  const { labelOf: outcomeLabel } = useOutcomeTypes();
  return useCallback(
    (key: string, value: string): string | null | undefined => {
      switch (key) {
        case 'reasonCode':
          return reasonLabel(value);
        case 'outcomeCode':
          return outcomeLabel(value);
        case 'movementType':
          return t(`types.${value}`, { defaultValue: value });
        case 'voidedMovementId':
          return null;
        default:
          return undefined;
      }
    },
    [reasonLabel, outcomeLabel, t],
  );
}
