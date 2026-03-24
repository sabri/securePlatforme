import { useState, type FormEvent } from 'react';
import { useAppDispatch, useAppSelector } from '../hooks/useRedux';
import { loginUser, clearError } from '../store/slices/authSlice';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { unwrapResult } from '@reduxjs/toolkit';

export default function LoginPage() {
  const dispatch = useAppDispatch();
  const { error: reduxError } = useAppSelector((state) => state.auth);
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    dispatch(clearError());
    setLoading(true);

    try {
      unwrapResult(await dispatch(loginUser({ email, password })));
      const returnUrl = searchParams.get('returnUrl');
      if (returnUrl && returnUrl.startsWith('http://localhost:')) {
        window.location.href = returnUrl;
      } else {
        navigate('/dashboard');
      }
    } catch (err: any) {
      setError(typeof err === 'string' ? err : reduxError || 'Login failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={styles.container}>
      <div style={styles.card}>
        <h1 style={styles.title}>🔐 Sign In</h1>
        <p style={styles.subtitle}>SecurePlatform — JWT Authentication Demo</p>

        {error && <div style={styles.error}>{error}</div>}

        <form onSubmit={handleSubmit}>
          <div style={styles.field}>
            <label>Email</label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              style={styles.input}
              placeholder="admin@secureplatform.com"
            />
          </div>

          <div style={styles.field}>
            <label>Password</label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              style={styles.input}
              placeholder="Admin123!"
            />
          </div>

          <button type="submit" disabled={loading} style={styles.button}>
            {loading ? 'Signing in...' : 'Sign In'}
          </button>
        </form>

        <p style={styles.forgotLink}>
          <Link to="/forgot-password">Forgot your password?</Link>
        </p>

        <div style={styles.divider}>
          <span style={styles.dividerText}>or continue with</span>
        </div>

        {/* ═══════════════════════════════════════════════════════
            [SECURITY: BFF PATTERN] — OAuth links point to the
            same origin (/api/oauth/*) via the Vite proxy. The
            backend URL is never exposed to the client.
            ═══════════════════════════════════════════════════════ */}
        <div style={styles.oauthRow}>
          <a href="/api/oauth/google" style={styles.oauthButton}>
            Google
          </a>
          <a href="/api/oauth/github" style={styles.oauthButtonDark}>
            GitHub
          </a>
        </div>

        <p style={styles.link}>
          Don't have an account? <Link to="/register">Register here</Link>
        </p>
      </div>
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  container: {
    display: 'flex', justifyContent: 'center', alignItems: 'center',
    minHeight: '100vh', background: '#f0f2f5',
  },
  card: {
    background: 'white', padding: '2rem', borderRadius: '12px',
    boxShadow: '0 4px 24px rgba(0,0,0,0.1)', width: '100%', maxWidth: '400px',
  },
  title: { margin: 0, fontSize: '1.8rem', textAlign: 'center' as const },
  subtitle: { textAlign: 'center' as const, color: '#666', marginBottom: '1.5rem' },
  field: { marginBottom: '1rem' },
  input: {
    width: '100%', padding: '0.75rem', border: '1px solid #ddd',
    borderRadius: '8px', fontSize: '1rem', boxSizing: 'border-box' as const,
  },
  button: {
    width: '100%', padding: '0.75rem', background: '#4f46e5', color: 'white',
    border: 'none', borderRadius: '8px', fontSize: '1rem', cursor: 'pointer',
    marginTop: '0.5rem',
  },
  error: {
    background: '#fee2e2', color: '#dc2626', padding: '0.75rem',
    borderRadius: '8px', marginBottom: '1rem', textAlign: 'center' as const,
  },
  forgotLink: { textAlign: 'right' as const, marginTop: '0.5rem', fontSize: '0.9rem' },
  divider: {
    textAlign: 'center' as const, margin: '1.5rem 0',
    borderBottom: '1px solid #ddd', lineHeight: '0.1em',
  },
  dividerText: {
    background: 'white', padding: '0 0.75rem', color: '#999', fontSize: '0.85rem',
  },
  oauthRow: { display: 'flex', gap: '0.5rem', marginBottom: '1rem' },
  oauthButton: {
    flex: 1, padding: '0.75rem', textAlign: 'center' as const,
    border: '1px solid #ddd', borderRadius: '8px', textDecoration: 'none',
    color: '#333', fontWeight: 'bold', fontSize: '0.95rem',
  },
  oauthButtonDark: {
    flex: 1, padding: '0.75rem', textAlign: 'center' as const,
    border: 'none', borderRadius: '8px', textDecoration: 'none',
    background: '#24292e', color: 'white', fontWeight: 'bold', fontSize: '0.95rem',
  },
  link: { textAlign: 'center' as const, marginTop: '1rem' },
};
