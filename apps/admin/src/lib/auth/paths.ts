/**
 * Auth-related routes. `reset-password` and `accept-invitation` are fixed by the links the API emails
 * (`PublicLinks` in the Platform module); keep them in sync.
 */
export const paths = {
  home: '/',
  login: '/login',
  loginMfa: '/login/mfa',
  forgotPassword: '/forgot-password',
  resetPassword: '/reset-password',
  acceptInvitation: '/accept-invitation',
  organizations: '/organizations',
  mfaEnroll: '/mfa/enroll',
  security: '/account/security',
  staff: '/platform/staff',
} as const;

/** Pages reachable without a session. */
export const PUBLIC_PATHS: readonly string[] = [
  paths.login,
  paths.loginMfa,
  paths.forgotPassword,
  paths.resetPassword,
  paths.acceptInvitation,
];

export function isPublicPath(pathname: string): boolean {
  return PUBLIC_PATHS.includes(pathname);
}
