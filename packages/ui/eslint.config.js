import react from '@shelter/config/eslint/react';

export default [
  ...react,
  // Library of primitives, not an HMR boundary: exporting variants next to components is fine.
  { rules: { 'react-refresh/only-export-components': 'off' } },
];
