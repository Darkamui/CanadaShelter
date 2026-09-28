import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router';
import { readFragment } from './navigation';

/**
 * Reads the secret an emailed link carries in its fragment (`#token=…`), once, then removes it from the
 * address bar and history entry so it is not left behind on screen or in the browser's history.
 */
export function useLinkFragment(): URLSearchParams {
  const location = useLocation();
  const navigate = useNavigate();
  const [params] = useState(() => readFragment(location.hash));

  useEffect(() => {
    if (location.hash) {
      void navigate(
        { pathname: location.pathname, search: location.search, hash: '' },
        { replace: true, state: location.state as unknown },
      );
    }
  }, [location.hash, location.pathname, location.search, location.state, navigate]);

  return params;
}
