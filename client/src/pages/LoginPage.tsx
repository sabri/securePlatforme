import { useState, type FormEvent } from 'react';
import { useAppDispatch, useAppSelector } from '../hooks/useRedux';
import { loginUser, clearError } from '../store/slices/authSlice';
import { Link, useNavigate } from 'react-router-dom';
import { unwrapResult } from '@reduxjs/toolkit';

export default function LoginPage() {
  const dispatch = useAppDispatch();
  const { error: reduxError } = useAppSelector((state) => state.auth);
  const navigate = useNavigate();
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
      navigate('/dashboard');
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
  link: { textAlign: 'center' as const, marginTop: '1rem' },
};
