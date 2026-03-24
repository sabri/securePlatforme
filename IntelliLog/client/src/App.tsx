import { useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Provider } from 'react-redux';
import { store } from './store';
import { useAppDispatch } from './hooks/useRedux';
import { initAuth } from './store/slices/authSlice';
import ProtectedRoute from './components/ProtectedRoute';
import DashboardPage from './pages/DashboardPage';
import LogsPage from './pages/LogsPage';
import DocumentsPage from './pages/DocumentsPage';
import SearchPage from './pages/SearchPage';
import WebhooksPage from './pages/WebhooksPage';

function AppRoutes() {
  const dispatch = useAppDispatch();

  useEffect(() => {
    dispatch(initAuth());
  }, [dispatch]);

  return (
    <Routes>
      <Route path="/" element={<ProtectedRoute><DashboardPage /></ProtectedRoute>} />
      <Route path="/logs" element={<ProtectedRoute><LogsPage /></ProtectedRoute>} />
      <Route path="/documents" element={<ProtectedRoute><DocumentsPage /></ProtectedRoute>} />
      <Route path="/search" element={<ProtectedRoute><SearchPage /></ProtectedRoute>} />
      <Route path="/webhooks" element={<ProtectedRoute><WebhooksPage /></ProtectedRoute>} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

function App() {
  return (
    <Provider store={store}>
      <BrowserRouter>
        <AppRoutes />
      </BrowserRouter>
    </Provider>
  );
}

export default App;
