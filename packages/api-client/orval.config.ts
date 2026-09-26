import { defineConfig } from 'orval';

// Input is exported by the backend build (see "generate" in package.json). Output is committed;
// CI regenerates and fails on any diff.
const input = { target: './openapi.json' };

export default defineConfig({
  hooks: {
    input,
    output: {
      mode: 'tags',
      target: './src/generated/hooks',
      schemas: './src/generated/model',
      client: 'react-query',
      httpClient: 'fetch',
      clean: true,
      formatter: 'prettier',
      override: {
        mutator: { path: './src/http/fetcher.ts', name: 'shelterFetch' },
        // Hooks resolve to the response body; failures throw ApiError (see fetcher.ts).
        fetch: { includeHttpResponseReturnType: false },
      },
    },
  },
  zod: {
    input,
    output: {
      mode: 'tags',
      target: './src/generated/zod',
      client: 'zod',
      fileExtension: '.ts',
      clean: true,
      formatter: 'prettier',
    },
  },
});
