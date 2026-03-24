import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useAppSelector } from '../hooks/useRedux';
import { searchApi } from '../services/api';
import type { SearchHit, ClassifyTextResult } from '../types';

export default function SearchPage() {
  const user = useAppSelector((s) => s.auth.user);

  const [query, setQuery] = useState('');
  const [results, setResults] = useState<SearchHit[]>([]);
  const [searching, setSearching] = useState(false);
  const [searched, setSearched] = useState(false);

  const [classifyText, setClassifyText] = useState('');
  const [classification, setClassification] = useState<ClassifyTextResult | null>(null);
  const [classifying, setClassifying] = useState(false);
  const [modelType, setModelType] = useState('severity');

  const handleSearch = async () => {
    if (!query.trim()) return;
    setSearching(true);
    setResults([]);
    setSearched(true);
    try {
      const res = await searchApi.search(query);
      setResults(res.data.hits);
    } catch { /* interceptor */ }
    setSearching(false);
  };

  const handleClassify = async () => {
    if (!classifyText.trim()) return;
    setClassifying(true);
    setClassification(null);
    try {
      const res = await searchApi.classify(classifyText, modelType);
      setClassification(res.data);
    } catch { /* interceptor */ }
    setClassifying(false);
  };

  return (
    <div style={styles.container}>
      <nav style={styles.nav}>
        <h2 style={{ margin: 0 }}>📊 IntelliLog</h2>
        <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
          <Link to="/" style={styles.navLink}>Dashboard</Link>
          <Link to="/logs" style={styles.navLink}>Logs</Link>
          <Link to="/documents" style={styles.navLink}>Documents</Link>
          <Link to="/search" style={{ ...styles.navLink, fontWeight: 700 }}>RAG Search</Link>
          <Link to="/webhooks" style={styles.navLink}>Webhooks</Link>
          <span style={{ color: '#666', fontSize: '0.85rem' }}>{user?.firstName} {user?.lastName}</span>
        </div>
      </nav>

      <main style={styles.main}>
        {/* RAG Search */}
        <div style={styles.card}>
          <h3>🔍 Semantic Search (RAG)</h3>
          <p style={{ color: '#666', fontSize: '0.85rem', margin: '0 0 1rem' }}>
            Search your knowledge base using AI-powered similarity matching.
          </p>
          <div style={{ display: 'flex', gap: '0.5rem' }}>
            <input
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
              placeholder="Ask a question or enter search terms..."
              style={{ ...styles.input, flex: 1 }}
            />
            <button onClick={handleSearch} disabled={searching} style={styles.primaryBtn}>
              {searching ? 'Searching...' : 'Search'}
            </button>
          </div>

          {searched && !searching && results.length === 0 && (
            <p style={{ color: '#666', marginTop: '1rem', fontSize: '0.85rem' }}>
              No results found. Try different keywords or ingest more documents.
            </p>
          )}

          {results.length > 0 && (
            <div style={{ marginTop: '1rem' }}>
              <h4 style={{ margin: '0 0 0.5rem' }}>Results ({results.length})</h4>
              {results.map((r, i) => (
                <div key={i} style={styles.resultCard}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '0.25rem' }}>
                    <strong>{r.title}</strong>
                    <span style={styles.scoreBadge}>{(r.score * 100).toFixed(1)}% match</span>
                  </div>
                  <span style={{ ...styles.badge, background: '#e0e7ff', color: '#4f46e5' }}>{r.category}</span>
                  <p style={{ margin: '0.25rem 0 0', fontSize: '0.85rem', color: '#555' }}>{r.snippet}</p>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Text Classification */}
        <div style={styles.card}>
          <h3>🏷️ Text Classification</h3>
          <p style={{ color: '#666', fontSize: '0.85rem', margin: '0 0 1rem' }}>
            Classify text into categories using the trained ML model.
          </p>
          <textarea
            value={classifyText}
            onChange={(e) => setClassifyText(e.target.value)}
            placeholder="Paste text to classify..."
            style={{ ...styles.input, height: '80px', resize: 'vertical', marginBottom: '0.5rem' }}
          />
          <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
            <select value={modelType} onChange={(e) => setModelType(e.target.value)} style={{ ...styles.input, width: 'auto' }}>
              <option value="severity">Severity</option>
              <option value="category">Category</option>
            </select>
          <button onClick={handleClassify} disabled={classifying} style={styles.primaryBtn}>
            {classifying ? 'Classifying...' : 'Classify'}
          </button>
          </div>

          {classification && (
            <div style={styles.resultBox}>
              <div style={{ display: 'flex', gap: '1.5rem', alignItems: 'center' }}>
                <div>
                  <span style={{ fontSize: '0.8rem', color: '#666' }}>Predicted Label</span>
                  <div style={{ fontSize: '1.1rem', fontWeight: 700, color: '#4f46e5' }}>{classification.predictedLabel}</div>
                </div>
                <div>
                  <span style={{ fontSize: '0.8rem', color: '#666' }}>Model</span>
                  <div style={{ fontSize: '1.1rem', fontWeight: 700 }}>{classification.modelAvailable ? 'Trained' : 'Not trained'}</div>
                </div>
              </div>
            </div>
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
  scoreBadge: {
    background: '#dcfce7', color: '#16a34a', padding: '0.15rem 0.5rem',
    borderRadius: '999px', fontSize: '0.75rem', fontWeight: 600,
  },
  badge: { padding: '0.15rem 0.5rem', borderRadius: '999px', fontSize: '0.75rem', fontWeight: 600 },
  resultCard: {
    borderBottom: '1px solid #f3f4f6', padding: '0.75rem 0',
  },
  resultBox: {
    marginTop: '0.75rem', background: '#f0fdf4', border: '1px solid #bbf7d0',
    padding: '1rem', borderRadius: '8px',
  },
};
