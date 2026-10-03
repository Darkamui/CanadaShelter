import { createBrowserRouter, Navigate, type RouteObject } from 'react-router';
import { AnimalCreatePage } from '../features/animals/routes/AnimalCreatePage';
import { AnimalDetailPage } from '../features/animals/routes/AnimalDetailPage';
import { AnimalsListPage } from '../features/animals/routes/AnimalsListPage';
import { LocationDetailPage } from '../features/locations/routes/LocationDetailPage';
import { LocationsPage } from '../features/locations/routes/LocationsPage';
import { PeopleListPage } from '../features/people/routes/PeopleListPage';
import { PersonCreatePage } from '../features/people/routes/PersonCreatePage';
import { PersonDetailPage } from '../features/people/routes/PersonDetailPage';
import { AcceptInvitationPage } from '../features/platform/routes/AcceptInvitationPage';
import { ForgotPasswordPage } from '../features/platform/routes/ForgotPasswordPage';
import { LoginPage } from '../features/platform/routes/LoginPage';
import { MfaChallengePage } from '../features/platform/routes/MfaChallengePage';
import { MfaEnrollPage } from '../features/platform/routes/MfaEnrollPage';
import { OrganizationPickerPage } from '../features/platform/routes/OrganizationPickerPage';
import { ResetPasswordPage } from '../features/platform/routes/ResetPasswordPage';
import { SecurityPage } from '../features/platform/routes/SecurityPage';
import { StaffPage } from '../features/platform/routes/StaffPage';
import { Permissions } from '../lib/auth/permissions';
import { paths } from '../lib/auth/paths';
import { RequireOrganization } from './auth/RequireOrganization';
import { RequirePermission } from './auth/RequirePermission';
import { RequireSession } from './auth/RequireSession';
import { AppShell } from './layout/AppShell';
import { AuthLayout } from './layout/AuthLayout';
import { HomePage } from './pages/HomePage';
import { ModulePlaceholderPage } from './pages/ModulePlaceholderPage';
import { NotFoundPage } from './pages/NotFoundPage';

export const routes: RouteObject[] = [
  {
    // Public: no session needed.
    element: <AuthLayout />,
    children: [
      { path: paths.login, element: <LoginPage /> },
      { path: paths.loginMfa, element: <MfaChallengePage /> },
      { path: paths.forgotPassword, element: <ForgotPasswordPage /> },
      { path: paths.resetPassword, element: <ResetPasswordPage /> },
      { path: paths.acceptInvitation, element: <AcceptInvitationPage /> },
    ],
  },
  {
    element: <RequireSession />,
    children: [
      {
        // Signed in, before an organization is open or MFA is set up.
        element: <AuthLayout />,
        children: [
          { path: paths.organizations, element: <OrganizationPickerPage /> },
          { path: paths.mfaEnroll, element: <MfaEnrollPage /> },
        ],
      },
      {
        element: <RequireOrganization />,
        children: [
          {
            element: <AppShell />,
            children: [
              { index: true, element: <HomePage /> },
              {
                path: 'animals',
                element: (
                  <RequirePermission permission={Permissions.animalRead}>
                    <AnimalsListPage />
                  </RequirePermission>
                ),
              },
              {
                path: 'animals/new',
                element: (
                  <RequirePermission permission={Permissions.animalWrite}>
                    <AnimalCreatePage />
                  </RequirePermission>
                ),
              },
              {
                path: 'animals/:animalId',
                element: (
                  <RequirePermission permission={Permissions.animalRead}>
                    <AnimalDetailPage />
                  </RequirePermission>
                ),
              },
              {
                path: 'people',
                element: (
                  <RequirePermission permission={Permissions.personRead}>
                    <PeopleListPage />
                  </RequirePermission>
                ),
              },
              {
                path: 'people/new',
                element: (
                  <RequirePermission permission={Permissions.personWrite}>
                    <PersonCreatePage />
                  </RequirePermission>
                ),
              },
              {
                path: 'people/:personId',
                element: (
                  <RequirePermission permission={Permissions.personRead}>
                    <PersonDetailPage />
                  </RequirePermission>
                ),
              },
              {
                path: 'operations',
                element: (
                  <RequirePermission permission={Permissions.locationRead}>
                    <LocationsPage />
                  </RequirePermission>
                ),
              },
              {
                path: 'operations/locations/:locationId',
                element: (
                  <RequirePermission permission={Permissions.locationRead}>
                    <LocationDetailPage />
                  </RequirePermission>
                ),
              },
              { path: 'platform', element: <Navigate to={paths.staff} replace /> },
              {
                path: paths.staff,
                element: (
                  <RequirePermission permission={Permissions.staffRead}>
                    <StaffPage />
                  </RequirePermission>
                ),
              },
              { path: paths.security, element: <SecurityPage /> },
              { path: ':module', element: <ModulePlaceholderPage /> },
              { path: '*', element: <NotFoundPage /> },
            ],
          },
        ],
      },
    ],
  },
];

export const router = createBrowserRouter(routes);
