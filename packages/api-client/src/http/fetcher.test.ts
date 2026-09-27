import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError, shelterFetch } from './fetcher';

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
});
