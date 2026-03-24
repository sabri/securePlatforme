import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { authApi } from '../services/api';

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [codeSent, setCodeSent] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setMessage('');
    setLoading(true);

    try {
      const res = await authApi.forgotPassword({ email });
      setMessage(res.data.message || 'If that email is registered, a reset code has been sent.');
      setCodeSent(true);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Something went wrong.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={styles.container}>
      <div style={styles.card}>
        <h1 style={styles.title}>🔑 Forgot Password</h1>
        <p style={styles.subtitle}>Enter your email to receive a reset code</p>

        {error && <div style={styles.error}>{error}</div>}
        {message && <div style={styles.success}>{message}</div>}

        <form onSubmit={handleSubmit}>
          <div style={styles.field}>
            <label>Email</label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              style={styles.input}
              placeholder="your@email.com"
            />
          </div>

          <button type="submit" disabled={loading} style={styles.button}>
            {loading ? 'Sending...' : 'Send Reset Code'}
          </button>
        </form>

        {codeSent && (
          <p style={styles.link}>
            <Link to={`/reset-password?email=${encodeURIComponent(email)}`}>
              I have the code → Reset Password
            </Link>
          </p>
        )}

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
  success: {
    background: '#dcfce7', color: '#16a34a', padding: '0.75rem',
    borderRadius: '8px', marginBottom: '1rem', textAlign: 'center' as const,
  },
  link: { textAlign: 'center' as const, marginTop: '1rem' },
};
