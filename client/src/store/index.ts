import { configureStore } from '@reduxjs/toolkit';
import authReducer from './slices/authSlice';

// ─── Store ──────────────────────────────────────────────────
// Add more slices here as the app grows (e.g. aiSlice, notificationsSlice)
export const store = configureStore({
  reducer: {
    auth: authReducer,
    // ai: aiReducer,       ← future expansion
    // notifications: ...   ← future expansion
  },
});

// ─── Typed hooks (use these instead of plain useDispatch/useSelector) ───
export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;
