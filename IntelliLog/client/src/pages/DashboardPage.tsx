import { useEffect, useState } from 'react';
import { useAppDispatch, useAppSelector } from '../hooks/useRedux';
import { loadDashboard } from '../store/slices/dashboardSlice';
import { Link } from 'react-router-dom';
import { dataApi } from '../services/api';

export default function DashboardPage() {
  const dispatch = useAppDispatch();
  const { logs, logCount, docCount, webhooks, isLoading, error } = useAppSelector((s) => s.dashboard);
  const user = useAppSelector((s) => s.auth.user);
  const [genMsg, setGenMsg] = useState('');
  const [trainMsg, setTrainMsg] = useState('');

  useEffect(() => {
    dispatch(loadDashboard());
  }, [dispatch]);

  const handleGenerate = async () => {
    setGenMsg('Generating...');
    try {
      const res = await dataApi.generate(200, 30);
      setGenMsg(`Generated ${res.data.logsGenerated} logs and ${res.data.documentsGenerated} documents.`);
      dispatch(loadDashboard());
    } catch {
      setGenMsg('Generation failed.');
    }
  };

  const handleTrain = async (modelType: string) => {
    setTrainMsg(`Training ${modelType} model...`);
    try {
      const res = await dataApi.train(modelType);
      setTrainMsg(res.data.message);
    } catch {
      setTrainMsg('Training failed.');
    }
  };

  if (isLoading) return <div style={styles.container}><p>Loading...</p></div>;
  if (error) return <div style={styles.container}><p style={{ color: '#ef4444' }}>{error}</p></div>;

  const criticalLogs = logs.filter((l) => l.severity === 'Critical');

  return (
    <div style={styles.container}>
      <nav style={styles.nav}>
        <h2 style={{ margin: 0 }}>📊 IntelliLog</h2>
        <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
          <Link to="/" style={styles.navLink}>Dashboard</Link>
          <Link to="/logs" style={styles.navLink}>Logs</Link>
          <Link to="/documents" style={styles.navLink}>Documents</Link>
          <Link to="/search" style={styles.navLink}>RAG Search</Link>
          <Link to="/webhooks" style={styles.navLink}>Webhooks</Link>
          <span style={{ color: '#666', fontSize: '0.85rem' }}>
            {user?.firstName} {user?.lastName}
          </span>
          <a href="http://localhost:5173/dashboard" style={styles.backLink}>← SecurePlatform</a>
        </div>
      </nav>

      <main style={styles.main}>
        {/* Stats Cards */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '1rem', marginBottom: '1.5rem' }}>
          <div style={{ ...styles.card, textAlign: 'center' }}>
            <h3 style={{ margin: '0 0 0.5rem 0', color: '#6366f1' }}>{logCount}</h3>
            <p style={{ margin: 0, color: '#666', fontSize: '0.85rem' }}>Total Logs</p>
          </div>
          <div style={{ ...styles.card, textAlign: 'center' }}>
            <h3 style={{ margin: '0 0 0.5rem 0', color: '#ef4444' }}>{criticalLogs.length}</h3>
            <p style={{ margin: 0, color: '#666', fontSize: '0.85rem' }}>Critical (recent)</p>
          </div>
          <div style={{ ...styles.card, textAlign: 'center' }}>
            <h3 style={{ margin: '0 0 0.5rem 0', color: '#10b981' }}>{docCount}</h3>
            <p style={{ margin: 0, color: '#666', fontSize: '0.85rem' }}>Documents</p>
          </div>
          <div style={{ ...styles.card, textAlign: 'center' }}>
            <h3 style={{ margin: '0 0 0.5rem 0', color: '#f59e0b' }}>{webhooks.length}</h3>
            <p style={{ margin: 0, color: '#666', fontSize: '0.85rem' }}>Webhooks</p>
          </div>
        </div>

        {/* Data Generation Card */}
        <div style={styles.card}>
          <h3>🔄 Data Generation & ML Training</h3>
          <p style={{ fontSize: '0.85rem', color: '#666' }}>
            Generate synthetic logs & documents, then train ML.NET classifiers.
          </p>
          <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
            <button onClick={handleGenerate} style={styles.primaryBtn}>Generate Data</button>
            <button onClick={() => handleTrain('log')} style={styles.secondaryBtn}>Train Log Model</button>
            <button onClick={() => handleTrain('document')} style={styles.secondaryBtn}>Train Doc Model</button>
          </div>
          {genMsg && <div style={styles.infoBox}>{genMsg}</div>}
          {trainMsg && <div style={styles.successBox}>{trainMsg}</div>}
        </div>

        {/* Recent Logs Card */}
        <div style={styles.card}>
          <h3>📋 Recent Logs</h3>
          {logs.length === 0 ? (
            <p style={{ color: '#666', fontSize: '0.85rem' }}>No logs yet. Generate some data first.</p>
          ) : (
            <div style={{ maxHeight: '300px', overflow: 'auto' }}>
              <table style={{ width: '100%', fontSize: '0.8rem', borderCollapse: 'collapse' }}>
                <thead>
                  <tr style={{ borderBottom: '2px solid #e5e7eb', textAlign: 'left' }}>
                    <th style={{ padding: '0.5rem' }}>Time</th>
                    <th style={{ padding: '0.5rem' }}>Severity</th>
                    <th style={{ padding: '0.5rem' }}>Source</th>
                    <th style={{ padding: '0.5rem' }}>Message</th>
                  </tr>
                </thead>
                <tbody>
                  {logs.slice(0, 10).map((log) => (
                    <tr key={log.id} style={{ borderBottom: '1px solid #f3f4f6' }}>
                      <td style={{ padding: '0.4rem 0.5rem', whiteSpace: 'nowrap' }}>
                        {new Date(log.timestamp).toLocaleTimeString()}
                      </td>
                      <td style={{ padding: '0.4rem 0.5rem' }}>
                        <span style={{ ...styles.badge, background: severityColor(log.severity) }}>
                          {log.severity}
                        </span>
                      </td>
                      <td style={{ padding: '0.4rem 0.5rem' }}>{log.source}</td>
                      <td style={{ padding: '0.4rem 0.5rem', maxWidth: '300px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {log.message}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <Link to="/logs" style={{ fontSize: '0.85rem' }}>View all logs →</Link>
        </div>
      </main>
    </div>
  );
}

function severityColor(sev: string): string {
  switch (sev) {
    case 'Critical': return '#fecaca';
    case 'Error': return '#fed7aa';
    case 'Warning': return '#fef08a';
    default: return '#d1fae5';
  }
}

const styles: Record<string, React.CSSProperties> = {
  container: { minHeight: '100vh', background: '#f0f2f5' },
  nav: {
    background: 'white', padding: '1rem 2rem', display: 'flex',
    justifyContent: 'space-between', alignItems: 'center',
    boxShadow: '0 2px 8px rgba(0,0,0,0.08)',
  },
  navLink: {
    color: '#4f46e5', textDecoration: 'none', fontWeight: 500, fontSize: '0.9rem',
  },
  backLink: {
    color: '#ef4444', textDecoration: 'none', fontWeight: 500, fontSize: '0.85rem',
    padding: '0.3rem 0.75rem', border: '1px solid #ef4444', borderRadius: '6px',
  },
  main: { maxWidth: '1000px', margin: '2rem auto', padding: '0 1rem' },
  card: {
    background: 'white', padding: '1.5rem', borderRadius: '12px',
    boxShadow: '0 2px 12px rgba(0,0,0,0.06)', marginBottom: '1.5rem',
  },
  badge: {
    padding: '0.15rem 0.5rem', borderRadius: '999px', fontSize: '0.75rem', fontWeight: 600,
  },
  primaryBtn: {
    background: '#4f46e5', color: 'white', border: 'none',
    padding: '0.6rem 1.2rem', borderRadius: '8px', cursor: 'pointer',
  },
  secondaryBtn: {
    background: '#e0e7ff', color: '#4f46e5', border: 'none',
    padding: '0.6rem 1.2rem', borderRadius: '8px', cursor: 'pointer', fontWeight: 600,
  },
  infoBox: {
    marginTop: '0.75rem', background: '#eff6ff', border: '1px solid #bfdbfe',
    padding: '0.75rem', borderRadius: '8px', fontSize: '0.85rem',
  },
  successBox: {
    marginTop: '0.75rem', background: '#f0fdf4', border: '1px solid #bbf7d0',
    padding: '0.75rem', borderRadius: '8px', fontSize: '0.85rem',
  },
};
