import { TabCount, tabClass, TabNav } from '@shelter/ui/components/tab-nav';
import { useTranslation } from 'react-i18next';
import { NavLink } from 'react-router';

export type RecordTab = {
  /** Relative to the record's route: `.` for the overview, then `movements`, `history`… */
  to: string;
  label: string;
  count?: number;
};

/** The tabs along the bottom of a {@link RecordHeader}. Each tab is a nested route, so it can be bookmarked. */
export function RecordTabs({ tabs }: { tabs: RecordTab[] }) {
  const { t } = useTranslation('shell');
  if (tabs.length < 2) return null;

  return (
    <TabNav aria-label={t('record.tabs')}>
      {tabs.map(({ to, label, count }) => (
        <NavLink key={to} to={to} end={to === '.'} className={tabClass}>
          {label}
          {count !== undefined && <TabCount>{count}</TabCount>}
        </NavLink>
      ))}
    </TabNav>
  );
}
