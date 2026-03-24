import { useState, type FormEvent } from 'react';
import { Link, useSearchParams, useNavigate } from 'react-router-dom';
import { authApi } from '../services/api';

export default function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const [form, setForm] = useState({
    email: searchParams.get('email') || '',
    code: '',
    newPassword: '',
    confirmPassword: '',
  });
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setMessage('');

    if (form.newPassword !== form.confirmPassword) {
      setError('Passwords do not match');
      return;
    }

    setLoading(true);
    try {
      const res = await authApi.resetPassword(form);
      setMessage(res.data.message || 'Password reset successfully!');
      setTimeout(() => navigate('/login'), 2000);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Reset failed. Check your code and try again.');
    } finally {
      setLoading(false);
    }
  };

  const update = (field: string) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm({ ...form, [field]: e.target.value });

  return (
    <div style={styles.container}>
      <div style={styles.card}>
        <h1 style={styles.title}>🔄 Reset Password</h1>
        <p style={styles.subtitle}>Enter the code you received and your new password</p>

        {error && <div style={styles.error}>{error}</div>}
        {message && <div style={styles.success}>{message}</div>}

        <form onSubmit={handleSubmit}>
          <div style={styles.field}>
            <label>Email</label>
            <input
              type="email"
              value={form.email}
              onChange={update('email')}
              required
              style={styles.input}
            />
          </div>

          <div style={styles.field}>
            <label>Reset Code</label>
            <input
              type="text"
              value={form.code}
              onChange={update('code')}
              required
              style={styles.input}
              placeholder="Paste the code from your email"
            />
          </div>

          <div style={styles.field}>
            <label>New Password</label>
            <input
              type="password"
              value={form.newPassword}
              onChange={update('newPassword')}
              required
              style={styles.input}
            />
          </div>

          <div style={styles.field}>
            <label>Confirm Password</label>
            <input
              type="password"
              value={form.confirmPassword}
              onChange={update('confirmPassword')}
              required
              style={styles.input}
            />
          </div>

          <button type="submit" disabled={loading} style={styles.button}>
            {loading ? 'Resetting...' : 'Reset Password'}
          </button>
        </form>

        <p style={styles.link}>
          <Link to="/login">← Back to Sign In</Link>
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
  success: {
    background: '#dcfce7', color: '#16a34a', padding: '0.75rem',
    borderRadius: '8px', marginBottom: '1rem', textAlign: 'center' as const,
  },
  link: { textAlign: 'center' as const, marginTop: '1rem' },
};
