import type { ComponentProps, ReactNode } from 'react';
import { cn } from '../lib/utils';

/** First letters of the first two words of a name. */
export function initialsOf(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => word[0]?.toLocaleUpperCase() ?? '')
    .join('');
}

const sizes = {
  sm: 'size-8 rounded-full text-xs [&_svg]:size-4',
  md: 'size-10 rounded-full text-sm [&_svg]:size-5',
  lg: 'size-16 rounded-2xl text-xl [&_svg]:size-7',
} as const;

/** Decorative identity tile: initials or an icon (species, location kind). The name is always shown next to it. */
function Avatar({
  size = 'md',
  children,
  className,
  ...props
}: ComponentProps<'span'> & { size?: keyof typeof sizes; children: ReactNode }) {
  return (
    <span
      aria-hidden="true"
      data-slot="avatar"
      className={cn(
        'inline-flex shrink-0 items-center justify-center bg-secondary font-display font-semibold text-secondary-foreground',
        sizes[size],
        className,
      )}
      {...props}
    >
      {children}
    </span>
  );
}

export { Avatar };
