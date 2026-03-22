import { useState, type FormEvent } from 'react';
import { useAppDispatch, useAppSelector } from '../hooks/useRedux';
import { registerUser, clearError } from '../store/slices/authSlice';
import { Link, useNavigate } from 'react-router-dom';
import { unwrapResult } from '@reduxjs/toolkit';

export default function RegisterPage() {
  const dispatch = useAppDispatch();
  const { error: reduxError } = useAppSelector((state) => state.auth);
  const navigate = useNavigate();
  const [form, setForm] = useState({
    email: '', password: '', confirmPassword: '', firstName: '', lastName: '',
  });
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    dispatch(clearError());

    if (form.password !== form.confirmPassword) {
      setError('Passwords do not match');
      return;
    }

    setLoading(true);
    try {
      unwrapResult(await dispatch(registerUser(form)));
      navigate('/dashboard');
    } catch (err: any) {
      setError(typeof err === 'string' ? err : reduxError || 'Registration failed');
    } finally {
      setLoading(false);
    }
  };

  const update = (field: string) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm({ ...form, [field]: e.target.value });

  return (
    <div style={styles.container}>
      <div style={styles.card}>
        <h1 style={styles.title}>📝 Register</h1>

        {error && <div style={styles.error}>{error}</div>}

        <form onSubmit={handleSubmit}>
          <div style={{ display: 'flex', gap: '0.5rem' }}>
            <div style={styles.field}>
              <label>First Name</label>
              <input type="text" value={form.firstName} onChange={update('firstName')} required style={styles.input} />
            </div>
            <div style={styles.field}>
              <label>Last Name</label>
              <input type="text" value={form.lastName} onChange={update('lastName')} required style={styles.input} />
            </div>
          </div>

          <div style={styles.field}>
            <label>Email</label>
            <input type="email" value={form.email} onChange={update('email')} required style={styles.input} />
          </div>

          <div style={styles.field}>
            <label>Password</label>
            <input type="password" value={form.password} onChange={update('password')} required style={styles.input} />
          </div>

          <div style={styles.field}>
            <label>Confirm Password</label>
            <input type="password" value={form.confirmPassword} onChange={update('confirmPassword')} required style={styles.input} />
          </div>

          <button type="submit" disabled={loading} style={styles.button}>
            {loading ? 'Creating account...' : 'Create Account'}
          </button>
        </form>

        <p style={styles.link}>
          Already have an account? <Link to="/login">Sign in</Link>
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
    boxShadow: '0 4px 24px rgba(0,0,0,0.1)', width: '100%', maxWidth: '450px',
  },
  title: { margin: '0 0 1.5rem', fontSize: '1.8rem', textAlign: 'center' as const },
  field: { marginBottom: '1rem', flex: 1 },
  input: {
    width: '100%', padding: '0.75rem', border: '1px solid #ddd',
    borderRadius: '8px', fontSize: '1rem', boxSizing: 'border-box' as const,
  },
  button: {
    width: '100%', padding: '0.75rem', background: '#4f46e5', color: 'white',
    border: 'none', borderRadius: '8px', fontSize: '1rem', cursor: 'pointer',
  },
  error: {
    background: '#fee2e2', color: '#dc2626', padding: '0.75rem',
    borderRadius: '8px', marginBottom: '1rem', textAlign: 'center' as const,
  },
  link: { textAlign: 'center' as const, marginTop: '1rem' },
};
