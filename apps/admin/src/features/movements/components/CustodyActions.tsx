import type { AnimalResponse } from '@shelter/api-client/model';
import { Button } from '@shelter/ui/components/button';
import { toast } from '@shelter/ui/components/toaster';
import { ArrowRightLeft, LogIn, LogOut } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Permissions } from '../../../lib/auth/permissions';
import { hasPermission, useSession } from '../../../lib/auth/session';
import type { MovementKind } from '../model';
import { useRefreshCustody } from '../useRefreshCustody';
import { MovementDialog } from './MovementDialog';

/**
 * The custody buttons of an animal's record, with `movement.write`: intake when not in care (or after an outcome),
 * move and outcome while in care. Each opens the movement dialog; a recorded movement is confirmed by a toast.
 */
export function CustodyActions({
  animal,
  startIntake = false,
}: {
  animal: AnimalResponse;
  /** Opens the intake dialog at once (from the create page). */
  startIntake?: boolean;
}) {
  const { t } = useTranslation('movements');
  const { data: session } = useSession();
  const canWrite = hasPermission(session, Permissions.movementWrite);
  const inCare = animal.custodyStatus === 'in_care';
  const refresh = useRefreshCustody(animal.id);
  const [kind, setKind] = useState<MovementKind | undefined>(
    startIntake && canWrite && !inCare ? 'intake' : undefined,
  );

  if (!canWrite) return null;

  return (
    <>
      {inCare ? (
        <>
          <Button variant="outline" onClick={() => setKind('outcome')}>
            <LogOut aria-hidden="true" />
            {t('actions.outcome')}
          </Button>
          <Button onClick={() => setKind('relocation')}>
            <ArrowRightLeft aria-hidden="true" />
            {t('actions.relocation')}
          </Button>
        </>
      ) : (
        <Button onClick={() => setKind('intake')}>
          <LogIn aria-hidden="true" />
          {t('actions.intake')}
        </Button>
      )}
      <MovementDialog
        animal={animal}
        kind={kind}
        onClose={() => setKind(undefined)}
        onRecorded={async (recorded) => {
          setKind(undefined);
          toast.success(t(`recorded.${recorded}`));
          await refresh();
        }}
        onConflict={refresh}
      />
    </>
  );
}
