# Frontend Lint Fixes Plan

## Goal
Fix all 57 ESLint problems in the frontend application to ensure a clean build and compliance with React 19 / TypeScript strict mode standards.

## Tasks
- [x] Task 1: Fix `react-hooks/set-state-in-effect` in all `use*.ts` hooks and pages. -> Verify: Run ESLint.
- [x] Task 2: Fix unused variables and `preserve-caught-error` across the codebase. -> Verify: Run ESLint.
- [x] Task 3: Fix `no-explicit-any` usages. -> Verify: Run ESLint.
- [x] Task 4: Fix `react-refresh/only-export-components` in `useChat.tsx`. -> Verify: Run ESLint.
- [x] Task 5: Run `npm run lint` and `npm run build` to verify zero errors.

## Done When
- `npm run lint` returns 0 errors.