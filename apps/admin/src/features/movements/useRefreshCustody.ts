import {
  getGetAnimalQueryKey,
  getGetAnimalTimelineQueryKey,
  getListAnimalsQueryKey,
} from '@shelter/api-client/hooks/animals';
import { getListMovementsQueryKey } from '@shelter/api-client/hooks/movements';
import { useQueryClient } from '@tanstack/react-query';
import { useCallback } from 'react';

/** Reloads everything a movement changes: the animal, its timeline and movements, and the animal list. */
export function useRefreshCustody(animalId: string) {
  const queryClient = useQueryClient();
  return useCallback(
    () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: getGetAnimalQueryKey(animalId) }),
        queryClient.invalidateQueries({ queryKey: getGetAnimalTimelineQueryKey(animalId) }),
        queryClient.invalidateQueries({ queryKey: getListMovementsQueryKey({ animalId }) }),
        queryClient.invalidateQueries({ queryKey: getListAnimalsQueryKey() }),
      ]),
    [queryClient, animalId],
  );
}
