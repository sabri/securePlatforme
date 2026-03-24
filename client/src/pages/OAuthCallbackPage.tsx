import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAppDispatch } from '../hooks/useRedux';
import { initAuth } from '../store/slices/authSlice';

// ═══════════════════════════════════════════════════════════════
// [SECURITY: HTTP-ONLY COOKIES + BFF PATTERN] — After OAuth
// login, the server redirects here with tokens already set in
// HTTP-only cookies. No tokens appear in the URL fragment — this
// eliminates token leakage via browser history, referrer headers,
// or XSS reading window.location.hash.
// ═══════════════════════════════════════════════════════════════
export default function OAuthCallbackPage() {
  const navigate = useNavigate();
  const dispatch = useAppDispatch();

  useEffect(() => {
    // Cookies are already set by the server's OAuth callback.
    // Just verify the session by calling /api/auth/me.
    dispatch(initAuth())
      .then((action) => {
        if (action.meta.requestStatus === 'fulfilled' && action.payload) {
          navigate('/dashboard');
        } else {
          navigate('/login');
        }
      })
      .catch(() => navigate('/login'));
  }, [dispatch, navigate]);

  return (
    <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '100vh' }}>
      <p>Completing login...</p>
    </div>
  );
}
