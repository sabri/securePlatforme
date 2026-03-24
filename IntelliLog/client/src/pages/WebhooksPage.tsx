import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAppSelector } from '../hooks/useRedux';
import { webhooksApi } from '../services/api';
import type { WebhookDto } from '../types';

export default function WebhooksPage() {
  const user = useAppSelector((s) => s.auth.user);
  const [subs, setSubs] = useState<WebhookDto[]>([]);
  const [loading, setLoading] = useState(false);

  const [name, setName] = useState('');
  const [url, setUrl] = useState('');
  const [eventType, setEventType] = useState('LogIngested');
  const [regMsg, setRegMsg] = useState('');

  const fetchWebhooks = async () => {
    setLoading(true);
    try {
      const res = await webhooksApi.getAll();
      setSubs(res.data.subscriptions);
    } catch { /* interceptor */ }
    setLoading(false);
  };

  useEffect(() => { fetchWebhooks(); }, []);

  const handleRegister = async () => {
    if (!name.trim() || !url.trim()) return;
    try {
      const res = await webhooksApi.register(name, url, eventType);
      setRegMsg(`Registered! Secret: ${res.data.secret}`);
      setName(''); setUrl('');
      fetchWebhooks();
    } catch {
      setRegMsg('Registration failed.');
    }
  };

  return (
    <div style={styles.container}>
      <nav style={styles.nav}>
        <h2 style={{ margin: 0 }}>📊 IntelliLog</h2>
        <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
          <Link to="/" style={styles.navLink}>Dashboard</Link>
          <Link to="/logs" style={styles.navLink}>Logs</Link>
          <Link to="/documents" style={styles.navLink}>Documents</Link>
          <Link to="/search" style={styles.navLink}>RAG Search</Link>
          <Link to="/webhooks" style={{ ...styles.navLink, fontWeight: 700 }}>Webhooks</Link>
          <span style={{ color: '#666', fontSize: '0.85rem' }}>{user?.firstName} {user?.lastName}</span>
        </div>
      </nav>

      <main style={styles.main}>
        {/* Register Webhook */}
        <div style={styles.card}>
          <h3>🔗 Register Webhook</h3>
          <p style={{ color: '#666', fontSize: '0.85rem', margin: '0 0 1rem' }}>
            Get notified when events occur in IntelliLog via HTTP POST callbacks.
          </p>
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Webhook name"
            style={{ ...styles.input, marginBottom: '0.5rem' }}
          />
          <input
            value={url}
            onChange={(e) => setUrl(e.target.value)}
            placeholder="https://your-server.com/webhook"
            style={{ ...styles.input, marginBottom: '0.5rem' }}
          />
          <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
            <select
              value={eventType}
              onChange={(e) => setEventType(e.target.value)}
              style={{ ...styles.input, width: 'auto', flex: 1 }}
            >
              <option value="LogIngested">Log Ingested</option>
              <option value="DocumentIngested">Document Ingested</option>
              <option value="AnomalyDetected">Anomaly Detected</option>
              <option value="ModelTrained">Model Trained</option>
            </select>
            <button onClick={handleRegister} style={styles.primaryBtn}>Register</button>
          </div>
          {regMsg && <div style={styles.infoBox}>{regMsg}</div>}
        </div>

        {/* Webhooks List */}
        <div style={styles.card}>
          <h3>📋 Webhook Subscriptions ({subs.length})</h3>
          {loading ? <p>Loading...</p> : subs.length === 0 ? (
            <p style={{ color: '#666', fontSize: '0.85rem' }}>No webhook subscriptions yet.</p>
          ) : (
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem' }}>
              <thead>
                <tr style={{ textAlign: 'left', borderBottom: '2px solid #e5e7eb' }}>
                  <th style={styles.th}>URL</th>
                  <th style={styles.th}>Event</th>
                  <th style={styles.th}>Status</th>
                  <th style={styles.th}>Created</th>
                </tr>
              </thead>
              <tbody>
                {subs.map((s) => (
                  <tr key={s.id} style={{ borderBottom: '1px solid #f3f4f6' }}>
                    <td style={styles.td}>
                      <code style={{ fontSize: '0.8rem', background: '#f3f4f6', padding: '0.15rem 0.4rem', borderRadius: '4px' }}>
                        {s.url}
                      </code>
                    </td>
                    <td style={styles.td}>
                      <span style={{ ...styles.badge, background: '#e0e7ff', color: '#4f46e5' }}>{s.eventType}</span>
                    </td>
                    <td style={styles.td}>
                      <span style={{
                        ...styles.badge,
                        background: s.isActive ? '#dcfce7' : '#fee2e2',
                        color: s.isActive ? '#16a34a' : '#dc2626',
                      }}>
                        {s.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                    <td style={styles.td}>{new Date(s.createdAt).toLocaleDateString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
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
  navLink: { color: '#4f46e5', textDecoration: 'none', fontWeight: 500, fontSize: '0.9rem' },
  main: { maxWidth: '900px', margin: '2rem auto', padding: '0 1rem' },
  card: {
    background: 'white', padding: '1.5rem', borderRadius: '12px',
    boxShadow: '0 2px 12px rgba(0,0,0,0.06)', marginBottom: '1.5rem',
  },
  input: {
    width: '100%', padding: '0.75rem', border: '1px solid #ddd',
    borderRadius: '8px', fontSize: '1rem', boxSizing: 'border-box' as const,
  },
  primaryBtn: {
    background: '#4f46e5', color: 'white', border: 'none',
    padding: '0.6rem 1.2rem', borderRadius: '8px', cursor: 'pointer', fontWeight: 600,
  },
  badge: { padding: '0.15rem 0.5rem', borderRadius: '999px', fontSize: '0.75rem', fontWeight: 600 },
  th: { padding: '0.75rem 0.5rem', fontWeight: 600 },
  td: { padding: '0.75rem 0.5rem' },
  infoBox: {
    marginTop: '0.75rem', background: '#eff6ff', border: '1px solid #bfdbfe',
    padding: '0.75rem', borderRadius: '8px', fontSize: '0.85rem',
  },
};
