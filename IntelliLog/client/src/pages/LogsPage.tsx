import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAppSelector } from '../hooks/useRedux';
import { logsApi } from '../services/api';
import type { LogDto } from '../types';

export default function LogsPage() {
  const user = useAppSelector((s) => s.auth.user);
  const [logs, setLogs] = useState<LogDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [severity, setSeverity] = useState('');
  const [loading, setLoading] = useState(false);

  const fetchLogs = async () => {
    setLoading(true);
    try {
      const res = await logsApi.getAll({ severity: severity || undefined, page, pageSize: 30 });
      setLogs(res.data.logs);
      setTotalCount(res.data.totalCount);
    } catch { /* handled by interceptor */ }
    setLoading(false);
  };

  useEffect(() => { fetchLogs(); }, [page, severity]);

  return (
    <div style={styles.container}>
      <nav style={styles.nav}>
        <h2 style={{ margin: 0 }}>📊 IntelliLog</h2>
        <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
          <Link to="/" style={styles.navLink}>Dashboard</Link>
          <Link to="/logs" style={{ ...styles.navLink, fontWeight: 700 }}>Logs</Link>
          <Link to="/documents" style={styles.navLink}>Documents</Link>
          <Link to="/search" style={styles.navLink}>RAG Search</Link>
          <Link to="/webhooks" style={styles.navLink}>Webhooks</Link>
          <span style={{ color: '#666', fontSize: '0.85rem' }}>{user?.firstName} {user?.lastName}</span>
        </div>
      </nav>

      <main style={styles.main}>
        <div style={styles.card}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
            <h3 style={{ margin: 0 }}>📋 Logs ({totalCount} total)</h3>
            <div style={{ display: 'flex', gap: '0.5rem' }}>
              {['', 'Info', 'Warning', 'Error', 'Critical'].map((s) => (
                <button
                  key={s}
                  onClick={() => { setSeverity(s); setPage(1); }}
                  style={{
                    ...styles.filterBtn,
                    background: severity === s ? '#4f46e5' : '#e5e7eb',
                    color: severity === s ? 'white' : '#333',
                  }}
                >
                  {s || 'All'}
                </button>
              ))}
            </div>
          </div>

          {loading ? <p>Loading...</p> : (
            <div style={{ maxHeight: '500px', overflow: 'auto' }}>
              <table style={{ width: '100%', fontSize: '0.8rem', borderCollapse: 'collapse' }}>
                <thead>
                  <tr style={{ borderBottom: '2px solid #e5e7eb', textAlign: 'left' }}>
                    <th style={{ padding: '0.5rem' }}>Time</th>
                    <th style={{ padding: '0.5rem' }}>Severity</th>
                    <th style={{ padding: '0.5rem' }}>Source</th>
                    <th style={{ padding: '0.5rem' }}>Message</th>
                    <th style={{ padding: '0.5rem' }}>Anomaly</th>
                  </tr>
                </thead>
                <tbody>
                  {logs.map((log) => (
                    <tr key={log.id} style={{ borderBottom: '1px solid #f3f4f6' }}>
                      <td style={{ padding: '0.4rem 0.5rem', whiteSpace: 'nowrap' }}>
                        {new Date(log.timestamp).toLocaleString()}
                      </td>
                      <td style={{ padding: '0.4rem 0.5rem' }}>
                        <span style={{ ...styles.badge, background: severityColor(log.severity) }}>{log.severity}</span>
                      </td>
                      <td style={{ padding: '0.4rem 0.5rem' }}>{log.source}</td>
                      <td style={{ padding: '0.4rem 0.5rem', maxWidth: '400px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {log.message}
                      </td>
                      <td style={{ padding: '0.4rem 0.5rem' }}>
                        {log.isAnomaly && <span style={{ color: '#ef4444', fontWeight: 600 }}>⚠ {log.anomalyScore.toFixed(2)}</span>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <div style={{ display: 'flex', gap: '0.5rem', marginTop: '1rem', justifyContent: 'center' }}>
            <button disabled={page <= 1} onClick={() => setPage(p => p - 1)} style={styles.pageBtn}>← Prev</button>
            <span style={{ padding: '0.5rem', fontSize: '0.85rem' }}>Page {page}</span>
            <button disabled={logs.length < 30} onClick={() => setPage(p => p + 1)} style={styles.pageBtn}>Next →</button>
          </div>
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
  navLink: { color: '#4f46e5', textDecoration: 'none', fontWeight: 500, fontSize: '0.9rem' },
  main: { maxWidth: '1100px', margin: '2rem auto', padding: '0 1rem' },
  card: {
    background: 'white', padding: '1.5rem', borderRadius: '12px',
    boxShadow: '0 2px 12px rgba(0,0,0,0.06)',
  },
  badge: { padding: '0.15rem 0.5rem', borderRadius: '999px', fontSize: '0.75rem', fontWeight: 600 },
  filterBtn: {
    border: 'none', padding: '0.4rem 0.8rem', borderRadius: '6px',
    cursor: 'pointer', fontSize: '0.8rem', fontWeight: 500,
  },
  pageBtn: {
    border: '1px solid #ddd', padding: '0.4rem 0.8rem', borderRadius: '6px',
    cursor: 'pointer', fontSize: '0.85rem', background: 'white',
  },
};
