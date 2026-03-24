import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import type { LogDto, DocumentDto, WebhookDto } from '../../types';
import { logsApi, documentsApi, webhooksApi } from '../../services/api';

interface DashboardState {
  logs: LogDto[];
  logCount: number;
  documents: DocumentDto[];
  docCount: number;
  webhooks: WebhookDto[];
  isLoading: boolean;
  error: string | null;
}

const initialState: DashboardState = {
  logs: [],
  logCount: 0,
  documents: [],
  docCount: 0,
  webhooks: [],
  isLoading: false,
  error: null,
};

export const loadDashboard = createAsyncThunk(
  'dashboard/load',
  async () => {
    const [logsRes, docsRes, webhooksRes] = await Promise.all([
      logsApi.getAll({ page: 1, pageSize: 20 }),
      documentsApi.getAll({ page: 1, pageSize: 10 }),
      webhooksApi.getAll(),
    ]);
    return {
      logs: logsRes.data.logs,
      logCount: logsRes.data.totalCount,
      documents: docsRes.data.documents,
      docCount: docsRes.data.totalCount,
      webhooks: webhooksRes.data.subscriptions,
    };
  }
);

const dashboardSlice = createSlice({
  name: 'dashboard',
  initialState,
  reducers: {},
  extraReducers: (builder) => {
    builder
      .addCase(loadDashboard.pending, (state) => {
        state.isLoading = true;
        state.error = null;
      })
      .addCase(loadDashboard.fulfilled, (state, action) => {
        state.isLoading = false;
        state.logs = action.payload.logs;
        state.logCount = action.payload.logCount;
        state.documents = action.payload.documents;
        state.docCount = action.payload.docCount;
        state.webhooks = action.payload.webhooks;
      })
      .addCase(loadDashboard.rejected, (state, action) => {
        state.isLoading = false;
        state.error = action.error.message || 'Failed to load dashboard';
      });
  },
});

export default dashboardSlice.reducer;
