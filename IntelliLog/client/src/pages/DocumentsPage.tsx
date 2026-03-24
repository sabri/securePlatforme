import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAppSelector } from '../hooks/useRedux';
import { documentsApi } from '../services/api';
import type { DocumentDto } from '../types';

export default function DocumentsPage() {
  const user = useAppSelector((s) => s.auth.user);
  const [docs, setDocs] = useState<DocumentDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [category, setCategory] = useState('');
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [ingestMsg, setIngestMsg] = useState('');

  const fetchDocs = async () => {
    setLoading(true);
    try {
      const res = await documentsApi.getAll({ category: category || undefined, page, pageSize: 20 });
      setDocs(res.data.documents);
      setTotalCount(res.data.totalCount);
    } catch { /* handled by interceptor */ }
    setLoading(false);
  };

  useEffect(() => { fetchDocs(); }, [page, category]);

  const handleIngest = async () => {
    if (!title.trim() || !content.trim()) return;
    try {
      const res = await documentsApi.ingest(title, content);
      setIngestMsg(`Document "${res.data.title}" ingested as ${res.data.category}.`);
      setTitle(''); setContent('');
      fetchDocs();
    } catch {
      setIngestMsg('Ingest failed.');
    }
  };

  return (
    <div style={styles.container}>
      <nav style={styles.nav}>
        <h2 style={{ margin: 0 }}>📊 IntelliLog</h2>
        <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
          <Link to="/" style={styles.navLink}>Dashboard</Link>
          <Link to="/logs" style={styles.navLink}>Logs</Link>
          <Link to="/documents" style={{ ...styles.navLink, fontWeight: 700 }}>Documents</Link>
          <Link to="/search" style={styles.navLink}>RAG Search</Link>
          <Link to="/webhooks" style={styles.navLink}>Webhooks</Link>
          <span style={{ color: '#666', fontSize: '0.85rem' }}>{user?.firstName} {user?.lastName}</span>
        </div>
      </nav>

      <main style={styles.main}>
        {/* Ingest Document Card */}
        <div style={styles.card}>
          <h3>📄 Ingest Document</h3>
          <input
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="Document title"
            style={{ ...styles.input, marginBottom: '0.5rem' }}
          />
          <textarea
            value={content}
            onChange={(e) => setContent(e.target.value)}
            placeholder="Document content..."
            style={{ ...styles.input, height: '100px', resize: 'vertical' }}
          />
          <button onClick={handleIngest} style={styles.primaryBtn}>Ingest</button>
          {ingestMsg && <div style={styles.infoBox}>{ingestMsg}</div>}
        </div>

        {/* Document List */}
        <div style={styles.card}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
            <h3 style={{ margin: 0 }}>📚 Documents ({totalCount})</h3>
            <div style={{ display: 'flex', gap: '0.5rem' }}>
              {['', 'General', 'Technical', 'Security', 'Business', 'Support'].map((c) => (
                <button
                  key={c}
                  onClick={() => { setCategory(c); setPage(1); }}
                  style={{
                    ...styles.filterBtn,
                    background: category === c ? '#4f46e5' : '#e5e7eb',
                    color: category === c ? 'white' : '#333',
                  }}
                >
                  {c || 'All'}
                </button>
              ))}
            </div>
          </div>

          {loading ? <p>Loading...</p> : docs.length === 0 ? (
            <p style={{ color: '#666', fontSize: '0.85rem' }}>No documents yet.</p>
          ) : (
            <div>
              {docs.map((doc) => (
                <div key={doc.id} style={{ borderBottom: '1px solid #f3f4f6', padding: '0.75rem 0' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <strong>{doc.title}</strong>
                    <span style={{ ...styles.badge, background: '#e0e7ff', color: '#4f46e5' }}>{doc.category}</span>
                  </div>
                  <p style={{ margin: '0.25rem 0 0', fontSize: '0.8rem', color: '#666' }}>{doc.snippet}</p>
                </div>
              ))}
            </div>
          )}

          <div style={{ display: 'flex', gap: '0.5rem', marginTop: '1rem', justifyContent: 'center' }}>
            <button disabled={page <= 1} onClick={() => setPage(p => p - 1)} style={styles.pageBtn}>← Prev</button>
            <span style={{ padding: '0.5rem', fontSize: '0.85rem' }}>Page {page}</span>
            <button disabled={docs.length < 20} onClick={() => setPage(p => p + 1)} style={styles.pageBtn}>Next →</button>
          </div>
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
    padding: '0.6rem 1.2rem', borderRadius: '8px', cursor: 'pointer', marginTop: '0.5rem',
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
  infoBox: {
    marginTop: '0.75rem', background: '#eff6ff', border: '1px solid #bfdbfe',
    padding: '0.75rem', borderRadius: '8px', fontSize: '0.85rem',
  },
};
