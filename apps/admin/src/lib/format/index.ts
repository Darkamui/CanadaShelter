/** Long date in the UI locale (`26 septembre 2026` / `September 26, 2026`). */
export function formatDate(value: string | Date, locale: string): string {
  return new Intl.DateTimeFormat(locale, { dateStyle: 'long' }).format(new Date(value));
}
