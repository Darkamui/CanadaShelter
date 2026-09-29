import i18next from 'eslint-plugin-i18next';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import tseslint from 'typescript-eslint';
import base from './base.js';

/**
 * React apps: hooks rules, fast-refresh safety, and no hard-coded user-facing
 * strings in JSX (CLAUDE.md hard rule 5). Tests are exempt from the i18n rule.
 */
export default tseslint.config(
  ...base,
  reactHooks.configs.flat['recommended-latest'],
  reactRefresh.configs.vite,
  {
    files: ['**/*.{ts,tsx}'],
    plugins: { i18next },
    rules: {
      'i18next/no-literal-string': [
        'error',
        {
          mode: 'jsx-only',
          callees: {
            // The plugin's defaults (replaced, not merged, by this option), then React Hook Form
            // methods whose string argument is a field name, not UI text.
            exclude: [
              'i18n(ext)?',
              't',
              'require',
              'addEventListener',
              'removeEventListener',
              'postMessage',
              'getElementById',
              'dispatch',
              'commit',
              'includes',
              'indexOf',
              'endsWith',
              'startsWith',
              'register',
              'watch',
              'setValue',
              'getValues',
              'setError',
              'clearErrors',
              'trigger',
              'resetField',
              'setFocus',
              'getFieldState',
            ],
          },
          'jsx-attributes': {
            include: ['^(aria-label|aria-description|title|placeholder|alt|label)$'],
          },
        },
      ],
    },
  },
  {
    files: ['**/*.test.{ts,tsx}', '**/tests/**', '**/e2e/**'],
    rules: { 'i18next/no-literal-string': 'off' },
  },
);
