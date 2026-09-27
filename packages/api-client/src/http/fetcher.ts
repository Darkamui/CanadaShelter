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

/**
 * Orval mutator: every generated call goes through here.
 * Same-origin cookies only (auth is cookie-based; tokens are never stored client-side).
 */
export async function shelterFetch<T>(url: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers);
  if (!headers.has('Accept')) {
    headers.set('Accept', 'application/json, application/problem+json');
  }

  const response = await fetch(url, { ...options, headers, credentials: 'same-origin' });
  const body = await readBody(response);

  if (!response.ok) {
    throw new ApiError(response.status, isProblemDetails(body) ? body : undefined);
  }
  return body as T;
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
