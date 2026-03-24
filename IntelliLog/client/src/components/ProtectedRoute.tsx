import type { ReactNode } from 'react';
import { useAppSelector } from '../hooks/useRedux';

const SECUREPLATFORM_LOGIN = 'http://localhost:5173/login';

export default function ProtectedRoute({ children }: { children: ReactNode }) {
  const { isAuthenticated, isLoading } = useAppSelector((s) => s.auth);

  if (isLoading) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '100vh' }}>
        <p>Verifying session...</p>
      </div>
    );
  }

  if (!isAuthenticated) {
    const returnUrl = encodeURIComponent(window.location.href);
    window.location.href = `${SECUREPLATFORM_LOGIN}?returnUrl=${returnUrl}`;
    return null;
  }

  return <>{children}</>;
}
