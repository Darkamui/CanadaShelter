/** Long date in the UI locale (`26 septembre 2026` / `September 26, 2026`). */
export function formatDate(value: string | Date, locale: string): string {
  return new Intl.DateTimeFormat(locale, { dateStyle: 'long' }).format(new Date(value));
}

/** A calendar date (`2024-05-01`) in the UI locale, without shifting it through the browser's time zone. */
export function formatDay(value: string, locale: string): string {
  return new Intl.DateTimeFormat(locale, { dateStyle: 'long', timeZone: 'UTC' }).format(
    new Date(`${value}T00:00:00Z`),
  );
}

/** Date and time in the UI locale and the browser's time zone. */
export function formatDateTime(value: string | Date, locale: string): string {
  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(
    new Date(value),
  );
}
