import { useGetAnimalPopulation } from '@shelter/api-client/hooks/animals';
import { useCallback, useMemo } from 'react';
import { Permissions } from '../../lib/auth/permissions';
import { hasPermission, useSession } from '../../lib/auth/session';

/**
 * Animals in care per location, loaded with `animal.read` only (`enabled`). `of` gives zero counts for a location the
 * server left out because no animal is at or below it.
 */
export function usePopulation() {
  const { data: session } = useSession();
  const enabled = hasPermission(session, Permissions.animalRead);
  const population = useGetAnimalPopulation({ query: { enabled } });
  const byId = useMemo(
    () => new Map((population.data ?? []).map((p) => [p.locationId, p])),
    [population.data],
  );
  const of = useCallback(
    (locationId: string) => byId.get(locationId) ?? { locationId, count: 0, subtreeCount: 0 },
    [byId],
  );
  return { enabled, ready: enabled && population.status === 'success', of };
}
