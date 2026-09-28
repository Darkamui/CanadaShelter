/** RFC 9457 problem details, as returned by Shelter.Host for every error. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  correlationId?: string;
  [extension: string]: unknown;
}

/** Thrown by every generated hook/function when the API answers with a non-2xx status. */
export class ApiError<TProblem = ProblemDetails> extends Error {
  constructor(
    readonly status: number,
    readonly problem: TProblem | undefined,
  ) {
    super(`API request failed with status ${status}`);
    this.name = 'ApiError';
  }
}

/** Orval uses this as the error type of generated queries and mutations. */
export type ErrorType<TProblem = ProblemDetails> = ApiError<TProblem>;

/** Readable cookie set by the API (`GET /api/platform/session/antiforgery`, login, logout). */
export const ANTIFORGERY_COOKIE = 'XSRF-TOKEN';
/** Header every unsafe request echoes the cookie in (double-submit, ADR 0018). */
export const ANTIFORGERY_HEADER = 'X-XSRF-TOKEN';
export const ANTIFORGERY_URL = '/api/platform/session/antiforgery';

const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS', 'TRACE']);

type UnauthorizedHandler = (url: string) => void;
let unauthorizedHandler: UnauthorizedHandler | undefined;
let pendingAntiforgery: Promise<void> | undefined;

/**
 * Called on every 401 answer (the session ended or never existed). The app decides what to do, such as
 * sending the user to the login page. Returns a function that removes the handler.
 */
export function onUnauthorized(handler: UnauthorizedHandler): () => void {
  unauthorizedHandler = handler;
  return () => {
    if (unauthorizedHandler === handler) unauthorizedHandler = undefined;
  };
}

/**
 * Orval mutator: every generated call goes through here.
 * Same-origin cookies only (auth is cookie-based; tokens are never stored client-side).
 * Unsafe methods carry the antiforgery header, fetching the cookie first when it is missing.
 */
export async function shelterFetch<T>(url: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers);
  if (!headers.has('Accept')) {
    headers.set('Accept', 'application/json, application/problem+json');
  }

  const method = (options.method ?? 'GET').toUpperCase();
  if (!SAFE_METHODS.has(method)) {
    const token = await antiforgeryToken();
    if (token) headers.set(ANTIFORGERY_HEADER, token);
  }

  const response = await fetch(url, { ...options, headers, credentials: 'same-origin' });
  const body = await readBody(response);

  if (!response.ok) {
    if (response.status === 401) unauthorizedHandler?.(url);
    throw new ApiError(response.status, isProblemDetails(body) ? body : undefined);
  }
  return body as T;
}

async function antiforgeryToken(): Promise<string | undefined> {
  const current = readCookie(ANTIFORGERY_COOKIE);
  if (current) return current;

  // One request for concurrent callers.
  pendingAntiforgery ??= fetch(ANTIFORGERY_URL, { credentials: 'same-origin' })
    .then(() => undefined)
    .finally(() => {
      pendingAntiforgery = undefined;
    });
  await pendingAntiforgery;
  return readCookie(ANTIFORGERY_COOKIE);
}

function readCookie(name: string): string | undefined {
  if (typeof document === 'undefined') return undefined;
  for (const part of document.cookie.split(';')) {
    const [key, ...value] = part.trim().split('=');
    if (key === name) return decodeURIComponent(value.join('='));
  }
  return undefined;
}

async function readBody(response: Response): Promise<unknown> {
  const text = await response.text();
  if (!text) return undefined;
  const contentType = response.headers.get('Content-Type') ?? '';
  return contentType.includes('json') ? (JSON.parse(text) as unknown) : text;
}

function isProblemDetails(body: unknown): body is ProblemDetails {
  return typeof body === 'object' && body !== null && ('title' in body || 'status' in body);
}
