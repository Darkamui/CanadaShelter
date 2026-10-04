import type { AnimalResponse } from '@shelter/api-client/model';
import { useOutletContext } from 'react-router';

export type AnimalRecordContext = { animal: AnimalResponse };

/** The animal the record layout loaded, for its tabs. */
export function useAnimalRecord() {
  return useOutletContext<AnimalRecordContext>();
}
