import { fireEvent, render, screen } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import { MemoryRouter } from 'react-router';
import { beforeEach, describe, expect, it } from 'vitest';
import i18n, { LOCALE_STORAGE_KEY } from '../../lib/i18n';
import { LanguageSwitcher } from './LanguageSwitcher';
import { Sidebar } from './Sidebar';

function renderShellParts() {
  return render(
    <I18nextProvider i18n={i18n}>
      <MemoryRouter>
        <Sidebar />
        <LanguageSwitcher />
      </MemoryRouter>
    </I18nextProvider>,
  );
}

describe('LanguageSwitcher', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('fr-CA');
  });

  it('renders in fr-CA by default and offers English', () => {
    renderShellParts();

    expect(document.documentElement.lang).toBe('fr-CA');
    expect(screen.getByRole('link', { name: 'Animaux' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'English' })).toHaveAttribute('lang', 'en-CA');
  });

  it('switches to en-CA, relabels the shell, and remembers the choice', async () => {
    renderShellParts();

    fireEvent.click(screen.getByRole('button', { name: 'English' }));

    expect(await screen.findByRole('link', { name: 'Animals' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Français' })).toHaveAttribute('lang', 'fr-CA');
    expect(document.documentElement.lang).toBe('en-CA');
    expect(localStorage.getItem(LOCALE_STORAGE_KEY)).toBe('en-CA');
  });
});
