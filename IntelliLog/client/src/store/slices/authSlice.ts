import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import type { UserDto } from '../../types/auth';
import { authApi } from '../../services/api';

interface AuthState {
  user: UserDto | null;
  isAuthenticated: boolean;
  isLoading: boolean;
}

const initialState: AuthState = {
  user: null,
  isAuthenticated: false,
  isLoading: true,
};

// Check session via SecurePlatform's /api/auth/me (JWT cookie shared across localhost)
export const initAuth = createAsyncThunk(
  'auth/init',
  async (_, { rejectWithValue }) => {
    try {
      await authApi.getCsrfToken();
      const res = await authApi.getMe();
      return res.data;
    } catch {
      return rejectWithValue('Not authenticated');
    }
  }
);

const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {},
  extraReducers: (builder) => {
    builder
      .addCase(initAuth.pending, (state) => {
        state.isLoading = true;
      })
      .addCase(initAuth.fulfilled, (state, action) => {
        state.isLoading = false;
        state.user = action.payload;
        state.isAuthenticated = true;
      })
      .addCase(initAuth.rejected, (state) => {
        state.isLoading = false;
        state.user = null;
        state.isAuthenticated = false;
      });
  },
});

export default authSlice.reducer;
