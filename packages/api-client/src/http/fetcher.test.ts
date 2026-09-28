import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  ANTIFORGERY_HEADER,
  ANTIFORGERY_URL,
  ApiError,
  onUnauthorized,
  shelterFetch,
} from './fetcher';

function stubFetch(response: Response) {
  const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(response);
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('shelterFetch', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('returns the JSON body and sends same-origin credentials', async () => {
    const fetchMock = stubFetch(Response.json({ status: 'ok' }));

    await expect(shelterFetch('/api/platform/ping', { method: 'GET' })).resolves.toEqual({
      status: 'ok',
    });
    expect(fetchMock.mock.calls[0]?.[1]?.credentials).toBe('same-origin');
  });

  it('throws ApiError carrying the problem details on failure', async () => {
    const problem = { title: 'Not Found', status: 404, correlationId: 'abc' };
    stubFetch(
      new Response(JSON.stringify(problem), {
        status: 404,
        headers: { 'Content-Type': 'application/problem+json' },
      }),
    );

    const error = await shelterFetch('/api/missing').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status: 404, problem });
  });

  it('throws ApiError without problem details for a non-problem body', async () => {
    stubFetch(new Response('Unhealthy', { status: 503 }));

    await expect(shelterFetch('/health/ready')).rejects.toMatchObject({
      status: 503,
      problem: undefined,
    });
  });

  it('does not send the antiforgery header on safe methods', async () => {
    vi.stubGlobal('document', { cookie: 'XSRF-TOKEN=abc' });
    const fetchMock = stubFetch(Response.json({ status: 'ok' }));

    await shelterFetch('/api/platform/ping', { method: 'GET' });

    expect(new Headers(fetchMock.mock.calls[0]?.[1]?.headers).has(ANTIFORGERY_HEADER)).toBe(false);
  });

  it('echoes the antiforgery cookie in the header on unsafe methods', async () => {
    vi.stubGlobal('document', { cookie: 'other=1; XSRF-TOKEN=CfDJ8%2Babc' });
    const fetchMock = stubFetch(new Response(null, { status: 204 }));

    await shelterFetch('/api/platform/session/logout', { method: 'POST' });

    expect(fetchMock).toHaveBeenCalledOnce();
    expect(new Headers(fetchMock.mock.calls[0]?.[1]?.headers).get(ANTIFORGERY_HEADER)).toBe(
      'CfDJ8+abc',
    );
  });

  it('fetches the antiforgery cookie first when it is missing', async () => {
    const cookieJar = { cookie: '' };
    vi.stubGlobal('document', cookieJar);
    const fetchMock = vi.fn<typeof fetch>().mockImplementation((input) => {
      if (String(input) === ANTIFORGERY_URL) {
        cookieJar.cookie = 'XSRF-TOKEN=fresh';
        return Promise.resolve(new Response(null, { status: 204 }));
      }
      return Promise.resolve(Response.json({ status: 'signedIn' }));
    });
    vi.stubGlobal('fetch', fetchMock);

    await shelterFetch('/api/platform/session/login', { method: 'POST', body: '{}' });

    expect(fetchMock.mock.calls.map((call) => String(call[0]))).toEqual([
      ANTIFORGERY_URL,
      '/api/platform/session/login',
    ]);
    expect(new Headers(fetchMock.mock.calls[1]?.[1]?.headers).get(ANTIFORGERY_HEADER)).toBe(
      'fresh',
    );
  });

  it('calls the unauthorized handler on 401 and still throws', async () => {
    const handler = vi.fn();
    const off = onUnauthorized(handler);
    vi.stubGlobal(
      'fetch',
      vi.fn<typeof fetch>().mockImplementation(() =>
        Promise.resolve(
          new Response(JSON.stringify({ title: 'Unauthorized', status: 401 }), {
            status: 401,
            headers: { 'Content-Type': 'application/problem+json' },
          }),
        ),
      ),
    );

    await expect(shelterFetch('/api/platform/session')).rejects.toMatchObject({ status: 401 });
    expect(handler).toHaveBeenCalledWith('/api/platform/session');

    off();
    await expect(shelterFetch('/api/platform/session')).rejects.toMatchObject({ status: 401 });
    expect(handler).toHaveBeenCalledOnce();
  });
});
