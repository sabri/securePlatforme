import { useState } from 'react';
import { useAppDispatch, useAppSelector } from '../hooks/useRedux';
import { logoutUser } from '../store/slices/authSlice';
import { useNavigate } from 'react-router-dom';
import { aiApi } from '../services/api';

export default function DashboardPage() {
  const dispatch = useAppDispatch();
  const user = useAppSelector((state) => state.auth.user);
  const navigate = useNavigate();
  const [prompt, setPrompt] = useState('');
  const [aiResponse, setAiResponse] = useState('');
  const [aiLoading, setAiLoading] = useState(false);

  const handleLogout = async () => {
    await dispatch(logoutUser());
    navigate('/login');
  };

  const handleAiQuery = async () => {
    if (!prompt.trim()) return;
    setAiLoading(true);
    try {
      const res = await aiApi.ask(prompt);
      setAiResponse(res.data.response);
    } catch (err: any) {
      setAiResponse('Error: ' + (err.response?.data?.message || err.message));
    }
    setAiLoading(false);
  };

  return (
    <div style={styles.container}>
      <nav style={styles.nav}>
        <h2 style={{ margin: 0 }}>🛡️ SecurePlatform</h2>
        <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
          <span>Welcome, <strong>{user?.firstName} {user?.lastName}</strong></span>
          <span style={styles.badge}>{user?.roles?.join(', ')}</span>
          <button onClick={handleLogout} style={styles.logoutBtn}>Logout</button>
        </div>
      </nav>

      <main style={styles.main}>
        {/* User Info Card */}
        <div style={styles.card}>
          <h3>👤 Your Profile</h3>
          <table style={{ width: '100%' }}>
            <tbody>
              <tr><td><strong>ID:</strong></td><td>{user?.id}</td></tr>
              <tr><td><strong>Email:</strong></td><td>{user?.email}</td></tr>
              <tr><td><strong>Name:</strong></td><td>{user?.firstName} {user?.lastName}</td></tr>
              <tr><td><strong>Roles:</strong></td><td>{user?.roles?.join(', ')}</td></tr>
            </tbody>
          </table>
        </div>

        {/* JWT Info Card */}
        <div style={styles.card}>
          <h3>🔑 JWT Token Info</h3>
          <p style={{ fontSize: '0.85rem', color: '#666' }}>
            Your access token is stored in <code>localStorage</code>. It's automatically
            attached to every API request via an Axios interceptor.
            When it expires, the refresh token is used to get a new one silently.
          </p>
          <details>
            <summary style={{ cursor: 'pointer', color: '#4f46e5' }}>View Access Token</summary>
            <pre style={styles.pre}>{localStorage.getItem('accessToken')}</pre>
          </details>
        </div>

        {/* AI Playground Card */}
        <div style={styles.card}>
          <h3>🤖 AI Playground (Placeholder)</h3>
          <p style={{ fontSize: '0.85rem', color: '#666' }}>
            This calls the AI/RAG endpoint. Currently returns mock data.
            Integrate Semantic Kernel or OpenAI to make it real.
          </p>
          <div style={{ display: 'flex', gap: '0.5rem' }}>
            <input
              value={prompt}
              onChange={(e) => setPrompt(e.target.value)}
              placeholder="Ask something..."
              style={{ ...styles.input, flex: 1 }}
              onKeyDown={(e) => e.key === 'Enter' && handleAiQuery()}
            />
            <button onClick={handleAiQuery} disabled={aiLoading} style={styles.aiBtn}>
              {aiLoading ? '...' : 'Ask'}
            </button>
          </div>
          {aiResponse && (
            <div style={styles.aiResponse}>{aiResponse}</div>
          )}
        </div>
      </main>
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  container: { minHeight: '100vh', background: '#f0f2f5' },
  nav: {
    background: 'white', padding: '1rem 2rem', display: 'flex',
    justifyContent: 'space-between', alignItems: 'center',
    boxShadow: '0 2px 8px rgba(0,0,0,0.08)',
  },
  badge: {
    background: '#e0e7ff', color: '#4f46e5', padding: '0.25rem 0.75rem',
    borderRadius: '999px', fontSize: '0.8rem', fontWeight: 600,
  },
  logoutBtn: {
    background: '#ef4444', color: 'white', border: 'none',
    padding: '0.5rem 1rem', borderRadius: '8px', cursor: 'pointer',
  },
  main: { maxWidth: '800px', margin: '2rem auto', padding: '0 1rem' },
  card: {
    background: 'white', padding: '1.5rem', borderRadius: '12px',
    boxShadow: '0 2px 12px rgba(0,0,0,0.06)', marginBottom: '1.5rem',
  },
  pre: {
    background: '#f8f9fa', padding: '1rem', borderRadius: '8px',
    wordBreak: 'break-all' as const, whiteSpace: 'pre-wrap' as const,
    fontSize: '0.75rem', maxHeight: '200px', overflow: 'auto',
  },
  input: {
    padding: '0.75rem', border: '1px solid #ddd', borderRadius: '8px',
    fontSize: '1rem',
  },
  aiBtn: {
    background: '#4f46e5', color: 'white', border: 'none',
    padding: '0.75rem 1.5rem', borderRadius: '8px', cursor: 'pointer',
  },
  aiResponse: {
    marginTop: '1rem', background: '#f0fdf4', border: '1px solid #bbf7d0',
    padding: '1rem', borderRadius: '8px', whiteSpace: 'pre-wrap' as const,
  },
};
