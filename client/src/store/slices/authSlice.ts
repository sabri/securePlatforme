import { createSlice, createAsyncThunk, type PayloadAction } from '@reduxjs/toolkit';
import type { UserDto, LoginRequest, RegisterRequest } from '../../types/auth';
import { authApi } from '../../services/api';

// ─── State Shape ─────────────────────────────────────────────
interface AuthState {
  user: UserDto | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: string | null;
}

const initialState: AuthState = {
  user: null,
  isAuthenticated: false,
  isLoading: true,   // true on first load to check for stored token
  error: null,
};

// ═══════════════════════════════════════════════════════════════
// [SECURITY: HTTP-ONLY COOKIES + BFF] — All auth state is derived
// from server-side cookies, NOT localStorage. The initAuth thunk
// calls GET /api/auth/me — if the HTTP-only cookie is valid, the
// server returns the user profile. No tokens are ever stored or
// read by JavaScript.
// ═══════════════════════════════════════════════════════════════

// ─── Async Thunks (side effects) ────────────────────────────
// These are the Redux Toolkit way to handle async logic.
// Each thunk auto-generates .pending / .fulfilled / .rejected actions.

export const initAuth = createAsyncThunk(
  'auth/init',
  async (_, { rejectWithValue }) => {
    try {
      // [SECURITY: CSRF] — Fetch CSRF token cookie before first request
      await authApi.getCsrfToken();

      // [SECURITY: HTTP-ONLY COOKIES] — Browser sends AccessToken cookie
      // automatically; no JS access to the token itself
      const res = await authApi.getMe();
      return res.data;
    } catch {
      return rejectWithValue('Session expired');
    }
  }
);

export const loginUser = createAsyncThunk(
  'auth/login',
  async (data: LoginRequest, { rejectWithValue }) => {
    try {
      const res = await authApi.login(data);
      if (res.data.succeeded) {
        // [SECURITY: HTTP-ONLY COOKIES] — Tokens are set in cookies
        // by the server response; no localStorage needed
        return res.data.user!;
      }
      return rejectWithValue(res.data.message || 'Login failed');
    } catch (err: any) {
      return rejectWithValue(err.response?.data?.message || 'Login failed');
    }
  }
);

export const registerUser = createAsyncThunk(
  'auth/register',
  async (data: RegisterRequest, { rejectWithValue }) => {
    try {
      const res = await authApi.register(data);
      if (res.data.succeeded) {
        // [SECURITY: HTTP-ONLY COOKIES] — Tokens set via cookies by server
        return res.data.user!;
      }
      return rejectWithValue(res.data.message || 'Registration failed');
    } catch (err: any) {
      return rejectWithValue(err.response?.data?.message || 'Registration failed');
    }
  }
);

export const logoutUser = createAsyncThunk(
  'auth/logout',
  async () => {
    try {
      // [SECURITY: HTTP-ONLY COOKIES] — Server clears the auth cookies
      await authApi.logout();
    } catch {
      // ignore — server might be unreachable
    }
    // No localStorage.clear() needed — tokens are in HTTP-only cookies
  }
);

// ─── Slice ──────────────────────────────────────────────────
const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    clearError(state) {
      state.error = null;
    },
    setUser(state, action: PayloadAction<UserDto | null>) {
      state.user = action.payload;
      state.isAuthenticated = !!action.payload;
    },
  },
  extraReducers: (builder) => {
    // ── initAuth ──
    builder
      .addCase(initAuth.pending, (state) => {
        state.isLoading = true;
      })
      .addCase(initAuth.fulfilled, (state, action) => {
        state.isLoading = false;
        state.user = action.payload;
        state.isAuthenticated = !!action.payload;
      })
      .addCase(initAuth.rejected, (state) => {
        state.isLoading = false;
        state.user = null;
        state.isAuthenticated = false;
      });

    // ── loginUser ──
    builder
      .addCase(loginUser.pending, (state) => {
        state.error = null;
      })
      .addCase(loginUser.fulfilled, (state, action) => {
        state.user = action.payload;
        state.isAuthenticated = true;
        state.error = null;
      })
      .addCase(loginUser.rejected, (state, action) => {
        state.error = action.payload as string;
      });

    // ── registerUser ──
    builder
      .addCase(registerUser.pending, (state) => {
        state.error = null;
      })
      .addCase(registerUser.fulfilled, (state, action) => {
        state.user = action.payload;
        state.isAuthenticated = true;
        state.error = null;
      })
      .addCase(registerUser.rejected, (state, action) => {
        state.error = action.payload as string;
      });

    // ── logoutUser ──
    builder
      .addCase(logoutUser.fulfilled, (state) => {
        state.user = null;
        state.isAuthenticated = false;
        state.error = null;
      });
  },
});

export const { clearError, setUser } = authSlice.actions;
export default authSlice.reducer;
