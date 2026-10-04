import { Alert } from '@shelter/ui/components/alert';
import type { ReactNode } from 'react';

/** A form-level message that stays until fixed: `error` is announced as an alert, `info` as a status. */
export function FormAlert({
  tone,
  title,
  children,
}: {
  tone: 'error' | 'info';
  title?: ReactNode;
  children?: ReactNode;
}) {
  return (
    <Alert tone={tone === 'error' ? 'danger' : 'info'} title={title}>
      {children}
    </Alert>
  );
}
