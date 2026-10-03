import {
  getListLocationsQueryKey,
  useArchiveLocation,
  useCreateLocation,
  useListLocationKinds,
  useListLocations,
  useUpdateLocation,
} from '@shelter/api-client/hooks/operations';
import type { LocationKindItem, LocationRequest } from '@shelter/api-client/model';
import { Button } from '@shelter/ui/components/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@shelter/ui/components/dialog';
import { useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Permissions } from '../../../lib/auth/permissions';
import { hasPermission, statusOf, useSession } from '../../../lib/auth/session';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { useLocalize } from '../../../lib/i18n/localized';
import { LocationForm } from '../components/LocationForm';
import {
  buildTree,
  flatten,
  fromLocation,
  subtreeIds,
  type LocationFormValues,
  type LocationNode,
} from '../model';

type Editing = { mode: 'create'; parentId: string | null } | { mode: 'edit'; node: LocationNode };

/** The organization's location tree: browse with `location.read`; create, edit, move and archive with `location.write`. */
export function LocationsPage() {
  const { t } = useTranslation('locations');
  const { data: session } = useSession();
  const canWrite = hasPermission(session, Permissions.locationWrite);
  const [includeArchived, setIncludeArchived] = useState(false);
  const locations = useListLocations({ includeArchived });
  const kinds = useListLocationKinds();
  const tree = useMemo(() => buildTree(locations.data ?? []), [locations.data]);
  const [editing, setEditing] = useState<Editing>();
  const [message, setMessage] = useState<{ tone: 'error' | 'info'; text: string }>();
  const queryClient = useQueryClient();
  const archive = useArchiveLocation();

  const refresh = () => queryClient.invalidateQueries({ queryKey: getListLocationsQueryKey() });
  const archiveLocation = (node: LocationNode) =>
    archive.mutate(
      { locationId: node.location.id },
      {
        onSuccess: async () => {
          setMessage({ tone: 'info', text: t('archived', { name: node.location.name }) });
          await refresh();
        },
        onError: (error) =>
          setMessage({
            tone: 'error',
            text: statusOf(error) === 409 ? t('archiveBlocked') : t('form.unexpectedError'),
          }),
      },
    );

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-2xl font-semibold">{t('title')}</h1>
        {canWrite && (
          <Button onClick={() => setEditing({ mode: 'create', parentId: null })}>{t('new')}</Button>
        )}
      </div>
      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={includeArchived}
          onChange={(e) => setIncludeArchived(e.target.checked)}
        />
        {t('showArchived')}
      </label>
      {message && <FormAlert tone={message.tone}>{message.text}</FormAlert>}
      {locations.isPending || kinds.isPending ? (
        <p role="status">{t('shell:list.loading')}</p>
      ) : locations.isError || kinds.isError ? (
        <FormAlert tone="error">{t('loadFailed')}</FormAlert>
      ) : tree.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t('empty')}</p>
      ) : (
        <LocationTree
          label={t('treeLabel')}
          nodes={tree}
          kinds={kinds.data}
          canWrite={canWrite}
          busy={archive.isPending}
          onAdd={(node) => setEditing({ mode: 'create', parentId: node.location.id })}
          onEdit={(node) => setEditing({ mode: 'edit', node })}
          onArchive={archiveLocation}
        />
      )}
      {canWrite && kinds.data && (
        <LocationDialog
          editing={editing}
          tree={tree}
          kinds={kinds.data}
          onClose={() => setEditing(undefined)}
          onSaved={async (name) => {
            setEditing(undefined);
            setMessage({ tone: 'info', text: t('saved', { name }) });
            await refresh();
          }}
        />
      )}
    </section>
  );
}

function LocationTree({
  label,
  nodes,
  kinds,
  canWrite,
  busy,
  onAdd,
  onEdit,
  onArchive,
}: {
  /** Accessible name of the root list only. */
  label?: string;
  nodes: readonly LocationNode[];
  kinds: readonly LocationKindItem[];
  canWrite: boolean;
  busy: boolean;
  onAdd: (node: LocationNode) => void;
  onEdit: (node: LocationNode) => void;
  onArchive: (node: LocationNode) => void;
}) {
  const { t } = useTranslation('locations');
  const localize = useLocalize();
  const kindLabel = (code: string) => {
    const kind = kinds.find((k) => k.code === code);
    return kind ? localize(kind.label) : code;
  };

  return (
    <ul className="flex flex-col gap-1" aria-label={label}>
      {nodes.map((node) => {
        const { location } = node;
        return (
          <li key={location.id} className="flex flex-col gap-1">
            <div className="flex flex-wrap items-center gap-2 rounded-md border px-3 py-2">
              <span className="font-medium">{location.name}</span>
              <span className="text-sm text-muted-foreground">{kindLabel(location.kindCode)}</span>
              {location.capacity !== null && (
                <span className="text-sm text-muted-foreground">
                  {t('capacity', { count: location.capacity })}
                </span>
              )}
              {location.isArchived && (
                <span className="text-xs text-muted-foreground">({t('archivedTag')})</span>
              )}
              {canWrite && !location.isArchived && (
                <span className="ml-auto flex gap-1">
                  <Button
                    size="sm"
                    variant="ghost"
                    aria-label={t('addInside', { name: location.name })}
                    onClick={() => onAdd(node)}
                  >
                    {t('add')}
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    aria-label={t('editNamed', { name: location.name })}
                    onClick={() => onEdit(node)}
                  >
                    {t('edit')}
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    disabled={busy}
                    aria-label={t('archiveNamed', { name: location.name })}
                    onClick={() => onArchive(node)}
                  >
                    {t('archive')}
                  </Button>
                </span>
              )}
            </div>
            {node.children.length > 0 && (
              <div className="pl-6">
                <LocationTree
                  nodes={node.children}
                  kinds={kinds}
                  canWrite={canWrite}
                  busy={busy}
                  onAdd={onAdd}
                  onEdit={onEdit}
                  onArchive={onArchive}
                />
              </div>
            )}
          </li>
        );
      })}
    </ul>
  );
}

function LocationDialog({
  editing,
  tree,
  kinds,
  onClose,
  onSaved,
}: {
  editing: Editing | undefined;
  tree: readonly LocationNode[];
  kinds: readonly LocationKindItem[];
  onClose: () => void;
  onSaved: (name: string) => Promise<void>;
}) {
  const { t } = useTranslation('locations');
  const create = useCreateLocation();
  const update = useUpdateLocation();

  // Archived locations cannot take children; a location cannot go inside itself or its descendants.
  const excluded = editing?.mode === 'edit' ? subtreeIds(editing.node) : new Set<string>();
  const parents = flatten(tree).filter(
    ({ location }) => !location.isArchived && !excluded.has(location.id),
  );
  const defaultValues: LocationFormValues =
    editing?.mode === 'edit'
      ? fromLocation(editing.node.location)
      : { name: '', kindCode: '', parentId: editing?.parentId ?? '', capacity: '' };

  const submit = async (data: LocationRequest) => {
    const saved =
      editing?.mode === 'edit'
        ? await update.mutateAsync({ locationId: editing.node.location.id, data })
        : await create.mutateAsync({ data });
    await onSaved(saved.name);
  };

  return (
    <Dialog open={editing !== undefined} onOpenChange={(open) => !open && onClose()}>
      <DialogContent closeLabel={t('close')}>
        <DialogHeader>
          <DialogTitle>{editing?.mode === 'edit' ? t('editTitle') : t('createTitle')}</DialogTitle>
          <DialogDescription>{t('dialogDescription')}</DialogDescription>
        </DialogHeader>
        {editing && (
          <LocationForm
            key={editing.mode === 'edit' ? editing.node.location.id : `new-${editing.parentId}`}
            defaultValues={defaultValues}
            kinds={kinds}
            parents={parents}
            submitLabel={editing.mode === 'edit' ? t('save') : t('create')}
            onSubmit={submit}
          />
        )}
      </DialogContent>
    </Dialog>
  );
}
