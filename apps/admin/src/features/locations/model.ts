import type { LocationItem, LocationRequest } from '@shelter/api-client/model';
import { z } from 'zod';
import { requiredText } from '../../lib/forms/schemas';

/** Server limits (`Location` on the server). */
export const NAME_MAX_LENGTH = 100;
export const MAX_CAPACITY = 10_000;

/** The location form. `parentId` and `capacity` are strings: `''` stands for "none". */
export const locationSchema = z.object({
  name: requiredText().max(NAME_MAX_LENGTH, { error: 'locations:form.tooLong' }),
  kindCode: requiredText(),
  parentId: z.string(),
  capacity: z
    .string()
    .trim()
    .refine((v) => v === '' || (/^\d+$/.test(v) && Number(v) <= MAX_CAPACITY), {
      error: 'locations:form.capacityRange',
    }),
});

export type LocationFormValues = z.infer<typeof locationSchema>;

export function toRequest(values: LocationFormValues): LocationRequest {
  return {
    name: values.name,
    kindCode: values.kindCode,
    parentId: values.parentId === '' ? null : values.parentId,
    capacity: values.capacity === '' ? null : Number(values.capacity),
  };
}

export function fromLocation(location: LocationItem): LocationFormValues {
  return {
    name: location.name,
    kindCode: location.kindCode,
    parentId: location.parentId ?? '',
    capacity: location.capacity === null ? '' : String(location.capacity),
  };
}

/** A location with its children, in the server's name order. */
export interface LocationNode {
  location: LocationItem;
  depth: number;
  children: LocationNode[];
}

/**
 * Nests the flat list by `parentId`. A location whose parent is not in the list (an archived parent when archived
 * ones are hidden) is shown at the top level.
 */
export function buildTree(locations: readonly LocationItem[]): LocationNode[] {
  const ids = new Set(locations.map((l) => l.id));
  const byParent = new Map<string | null, LocationItem[]>();
  for (const location of locations) {
    const parent =
      location.parentId !== null && ids.has(location.parentId) ? location.parentId : null;
    byParent.set(parent, [...(byParent.get(parent) ?? []), location]);
  }
  const build = (parent: string | null, depth: number): LocationNode[] =>
    (byParent.get(parent) ?? []).map((location) => ({
      location,
      depth,
      children: build(location.id, depth + 1),
    }));
  return build(null, 0);
}

/** Depth-first order, for the parent picker. */
export function flatten(nodes: readonly LocationNode[]): LocationNode[] {
  return nodes.flatMap((node) => [node, ...flatten(node.children)]);
}

/** The ids of `node` and everything below it: never valid parents for it. */
export function subtreeIds(node: LocationNode): Set<string> {
  return new Set(flatten([node]).map((n) => n.location.id));
}
