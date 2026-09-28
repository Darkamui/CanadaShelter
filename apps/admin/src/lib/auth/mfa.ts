/** Groups a base32 key by four for reading or typing. */
export function formatSharedKey(key: string): string {
  return (
    key
      .replace(/\s+/g, '')
      .match(/.{1,4}/g)
      ?.join(' ') ?? key
  );
}
