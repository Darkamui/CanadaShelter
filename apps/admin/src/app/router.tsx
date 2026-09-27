import { createBrowserRouter, type RouteObject } from 'react-router';
import { AnimalsPage } from '../features/animals/routes/AnimalsPage';
import { AppShell } from './layout/AppShell';
import { HomePage } from './pages/HomePage';
import { ModulePlaceholderPage } from './pages/ModulePlaceholderPage';
import { NotFoundPage } from './pages/NotFoundPage';

export const routes: RouteObject[] = [
  {
    element: <AppShell />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'animals', element: <AnimalsPage /> },
      { path: ':module', element: <ModulePlaceholderPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
];

export const router = createBrowserRouter(routes);
