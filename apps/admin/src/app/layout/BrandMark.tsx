import { cn } from '@shelter/ui/lib/utils';

/**
 * The app's mark: a collar tag (médaille) with a paw, the first thing a shelter reads on a found animal. The tag
 * takes `currentColor`; the paw is cut out in the surface color passed as `pawClassName`.
 */
export function BrandMark({
  className,
  pawClassName,
}: {
  className?: string;
  pawClassName?: string;
}) {
  return (
    <svg viewBox="0 0 32 32" aria-hidden="true" className={cn('shrink-0', className)}>
      <circle cx="16" cy="5.5" r="3" fill="none" stroke="currentColor" strokeWidth="2" />
      <circle cx="16" cy="18.5" r="11.5" fill="currentColor" />
      <g className={cn('fill-sidebar', pawClassName)}>
        <ellipse cx="16" cy="22" rx="4.4" ry="3.6" />
        <ellipse cx="10.6" cy="17.4" rx="1.8" ry="2.2" transform="rotate(-20 10.6 17.4)" />
        <ellipse cx="13.9" cy="14.4" rx="1.8" ry="2.3" transform="rotate(-6 13.9 14.4)" />
        <ellipse cx="18.1" cy="14.4" rx="1.8" ry="2.3" transform="rotate(6 18.1 14.4)" />
        <ellipse cx="21.4" cy="17.4" rx="1.8" ry="2.2" transform="rotate(20 21.4 17.4)" />
      </g>
    </svg>
  );
}
