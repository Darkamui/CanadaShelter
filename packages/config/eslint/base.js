import tseslint from 'typescript-eslint';

/** Shared ESLint flat config for all TypeScript packages. */
export default tseslint.config(
  {
    ignores: ['**/dist/**', '**/generated/**', '**/coverage/**', '**/playwright-report/**'],
  },
  ...tseslint.configs.recommended,
  {
    rules: {
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],
      '@typescript-eslint/consistent-type-imports': 'error',
    },
  },
);
