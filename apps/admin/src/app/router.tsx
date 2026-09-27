import { createBrowserRouter, type RouteObject } from 'react-router';
import { AppShell } from './layout/AppShell';
import { HomePage } from './pages/HomePage';
import { ModulePlaceholderPage } from './pages/ModulePlaceholderPage';
import { NotFoundPage } from './pages/NotFoundPage';

export const routes: RouteObject[] = [
  {
    element: <AppShell />,
    children: [
      { index: true, element: <HomePage /> },
      { path: ':module', element: <ModulePlaceholderPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
];

export const router = createBrowserRouter(routes);
