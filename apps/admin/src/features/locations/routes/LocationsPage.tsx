import {
  getListLocationsQueryKey,
  useArchiveLocation,
  useCreateLocation,
  useListLocationKinds,
  useListLocations,
  useUpdateLocation,
} from '@shelter/api-client/hooks/operations';
import type { LocationKindItem, LocationRequest } from '@shelter/api-client/model';
import { Badge } from '@shelter/ui/components/badge';
import { Button } from '@shelter/ui/components/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@shelter/ui/components/dialog';
import { EmptyState } from '@shelter/ui/components/empty-state';
import { PageHeader } from '@shelter/ui/components/page-header';
import { Skeleton } from '@shelter/ui/components/skeleton';
import { toast } from '@shelter/ui/components/toaster';
import { Tooltip, TooltipContent, TooltipTrigger } from '@shelter/ui/components/tooltip';
import { useQueryClient } from '@tanstack/react-query';
import { Archive, MapPin, Pencil, Plus } from 'lucide-react';
import { useMemo, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { Permissions } from '../../../lib/auth/permissions';
import { hasPermission, statusOf, useSession } from '../../../lib/auth/session';
import { FormAlert } from '../../../lib/forms/FormAlert';
import { CheckboxField } from '../../../lib/forms/FormField';
import { problemCodeOf } from '../../../lib/forms/serverErrors';
import { useLocalize } from '../../../lib/i18n/localized';
import { LocationForm } from '../components/LocationForm';
import { LocationKindIcon } from '../components/LocationKindIcon';
import {
  buildTree,
  flatten,
  fromLocation,
  subtreeIds,
  type LocationFormValues,
  type LocationNode,
} from '../model';
import { locationPath } from '../paths';
import { usePopulation } from '../usePopulation';

type Editing = { mode: 'create'; parentId: string | null } | { mode: 'edit'; node: LocationNode };

/**
 * The organization's location tree: browse with `location.read` (with `animal.read`, each location shows the animals
 * in care at or below it); create, edit, move and archive with `location.write`.
 */
export function LocationsPage() {
  const { t } = useTranslation('locations');
  const { data: session } = useSession();
  const canWrite = hasPermission(session, Permissions.locationWrite);
  const [includeArchived, setIncludeArchived] = useState(false);
  const locations = useListLocations({ includeArchived });
  const kinds = useListLocationKinds();
  const tree = useMemo(() => buildTree(locations.data ?? []), [locations.data]);
  const [editing, setEditing] = useState<Editing>();
  const [error, setError] = useState<string>();
  const queryClient = useQueryClient();
  const archive = useArchiveLocation();

  const refresh = () => queryClient.invalidateQueries({ queryKey: getListLocationsQueryKey() });
  const archiveLocation = (node: LocationNode) => {
    setError(undefined);
    archive.mutate(
      { locationId: node.location.id },
      {
        onSuccess: async () => {
          toast.success(t('archived', { name: node.location.name }));
          await refresh();
        },
        onError: (failure) =>
          setError(
            problemCodeOf(failure) === 'location.hasAnimals'
              ? t('archiveHasAnimals')
              : statusOf(failure) === 409
                ? t('archiveBlocked')
                : t('form.unexpectedError'),
          ),
      },
    );
  };
  const newLocation = canWrite && (
    <Button onClick={() => setEditing({ mode: 'create', parentId: null })}>
      <Plus aria-hidden="true" />
      {t('new')}
    </Button>
  );

  let body: ReactNode;
  let empty = false;
  if (locations.isPending || kinds.isPending) {
    body = <TreeSkeleton />;
  } else if (locations.isError || kinds.isError) {
    body = <FormAlert tone="error">{t('loadFailed')}</FormAlert>;
  } else if (tree.length === 0) {
    empty = true;
    body = (
      <EmptyState
        icon={<MapPin />}
        title={t('emptyTitle')}
        description={t('emptyBody')}
        action={newLocation}
      />
    );
  } else {
    body = (
      <div className="overflow-hidden rounded-xl border bg-card shadow-card">
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
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={t('title')}
        description={t('description')}
        actions={!empty && newLocation}
      />
      <div className="flex flex-col gap-3">
        <CheckboxField
          label={t('showArchived')}
          className="self-start"
          checked={includeArchived}
          onChange={(e) => setIncludeArchived(e.target.checked)}
        />
        {error && <FormAlert tone="error">{error}</FormAlert>}
        {body}
      </div>
      {canWrite && kinds.data && (
        <LocationDialog
          editing={editing}
          tree={tree}
          kinds={kinds.data}
          onClose={() => setEditing(undefined)}
          onSaved={async (name) => {
            setEditing(undefined);
            setError(undefined);
            toast.success(t('saved', { name }));
            await refresh();
          }}
        />
      )}
    </div>
  );
}

function TreeSkeleton() {
  const { t } = useTranslation('shell');
  return (
    <div role="status" className="flex flex-col rounded-xl border bg-card shadow-card">
      <span className="sr-only">{t('list.loading')}</span>
      {[0, 1, 2, 3].map((i) => (
        <div
          key={i}
          className="flex items-center gap-3 border-t px-4 py-3 first-of-type:border-t-0"
        >
          {i > 0 && <span className="w-7" />}
          <Skeleton className="size-8 rounded-lg" />
          <Skeleton className="h-4 w-40" />
        </div>
      ))}
    </div>
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
  const population = usePopulation();
  const kindLabel = (code: string) => {
    const kind = kinds.find((k) => k.code === code);
    return kind ? localize(kind.label) : code;
  };

  return (
    <ul aria-label={label} className={label && '[&>li:first-child>div]:border-t-0'}>
      {nodes.map((node) => {
        const { location, depth } = node;
        return (
          <li key={location.id}>
            <div className="flex min-h-14 items-stretch border-t pr-3 pl-4 transition-colors hover:bg-subtle">
              {/* One guide per level above: the structure shows without nesting boxes. */}
              {Array.from({ length: depth }, (_, i) => (
                <span
                  key={i}
                  aria-hidden="true"
                  className="relative w-7 shrink-0 before:absolute before:inset-y-0 before:left-4 before:w-px before:bg-border"
                />
              ))}
              <div className="flex min-w-0 flex-1 flex-wrap items-center gap-x-3 gap-y-1 py-2.5">
                <span
                  aria-hidden="true"
                  className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-secondary text-secondary-foreground [&_svg]:size-4"
                >
                  <LocationKindIcon code={location.kindCode} />
                </span>
                <div className="flex min-w-0 flex-1 basis-40 flex-col">
                  <span className="flex flex-wrap items-center gap-2">
                    <Link
                      to={locationPath(location.id)}
                      className="font-medium underline-offset-4 hover:underline"
                    >
                      {location.name}
                    </Link>
                    {location.isArchived && <Badge>{t('archivedBadge')}</Badge>}
                  </span>
                  <span className="text-[0.8125rem] text-muted-foreground">
                    {kindLabel(location.kindCode)}
                  </span>
                </div>
                <div className="flex items-center gap-x-5 text-sm">
                  {location.capacity !== null && (
                    <span className="text-muted-foreground">
                      {t('capacity', { count: location.capacity })}
                    </span>
                  )}
                  {population.ready && !location.isArchived && (
                    <span className="tabular min-w-20 text-right font-medium">
                      {t('animalCount', { count: population.of(location.id).subtreeCount })}
                    </span>
                  )}
                </div>
                {canWrite && !location.isArchived && (
                  <span className="flex gap-0.5">
                    <IconAction
                      label={t('addInside', { name: location.name })}
                      hint={t('add')}
                      onClick={() => onAdd(node)}
                    >
                      <Plus />
                    </IconAction>
                    <IconAction
                      label={t('editNamed', { name: location.name })}
                      hint={t('edit')}
                      onClick={() => onEdit(node)}
                    >
                      <Pencil />
                    </IconAction>
                    <IconAction
                      label={t('archiveNamed', { name: location.name })}
                      hint={t('archive')}
                      disabled={busy}
                      onClick={() => onArchive(node)}
                    >
                      <Archive />
                    </IconAction>
                  </span>
                )}
              </div>
            </div>
            {node.children.length > 0 && (
              <LocationTree
                nodes={node.children}
                kinds={kinds}
                canWrite={canWrite}
                busy={busy}
                onAdd={onAdd}
                onEdit={onEdit}
                onArchive={onArchive}
              />
            )}
          </li>
        );
      })}
    </ul>
  );
}

/** A row action: an icon button named for its location, with the short verb as a tooltip. */
function IconAction({
  label,
  hint,
  disabled,
  onClick,
  children,
}: {
  label: string;
  hint: string;
  disabled?: boolean;
  onClick: () => void;
  children: ReactNode;
}) {
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <Button
          size="icon-sm"
          variant="ghost"
          aria-label={label}
          disabled={disabled}
          onClick={onClick}
          className="text-muted-foreground hover:text-foreground"
        >
          {children}
        </Button>
      </TooltipTrigger>
      <TooltipContent>{hint}</TooltipContent>
    </Tooltip>
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
