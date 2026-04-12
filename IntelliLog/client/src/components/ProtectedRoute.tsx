import { useEffect, useRef, type ReactNode } from 'react';
import { useAppSelector } from '../hooks/useRedux';

const SECUREPLATFORM_LOGIN = 'http://localhost:5173/login';

export default function ProtectedRoute({ children }: { children: ReactNode }) {
  const { isAuthenticated, isLoading } = useAppSelector((s) => s.auth);
  const hasRedirected = useRef(false);

  useEffect(() => {
    if (!isLoading && !isAuthenticated && !hasRedirected.current) {
      hasRedirected.current = true;
      const returnUrl = encodeURIComponent(window.location.href);
      window.location.href = `${SECUREPLATFORM_LOGIN}?returnUrl=${returnUrl}`;
    }
  }, [isLoading, isAuthenticated]);

  if (isLoading || !isAuthenticated) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '100vh' }}>
        <p>{isLoading ? 'Verifying session...' : 'Redirecting to login...'}</p>
      </div>
    );
  }

  return <>{children}</>;
}
