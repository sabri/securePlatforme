import { useDispatch, useSelector } from 'react-redux';
import type { RootState, AppDispatch } from '../store';

// ─── Typed Redux hooks ──────────────────────────────────────
// Always use these instead of plain useDispatch / useSelector
// so you get full TypeScript inference on state and thunks.
export const useAppDispatch = useDispatch.withTypes<AppDispatch>();
export const useAppSelector = useSelector.withTypes<RootState>();
