---
description: Create a modern feature leveraging Server Components, Next.js 16 caching, and React 19 features.
---

# Workflow: Create Modern Feature (/create-modern-feature)

**Use case**: Use this workflow when a user wants to scaffold a completely new feature block, dashboard page, or complex component structure using modern Next.js 16 and React 19 capabilities.

## Phase 1: Planning and Component Boundaries

1. Analyze the objective. Identify which parts of the UI require interactivity (Client Components) and which simply display data (Server Components).
2. Propose the implementation strategy to the user. State clearly which files will have `"use client"` and which ones will handle data fetching natively.

## Phase 2: Create Data Flow and Server Actions

1. Scaffold Server Actions in a targeted `actions.ts`. 
2. Ensure you tag mutations via `revalidateTag` or `updateTag` natively on Next 16.
3. Determine if the new fetched data can benefit from `"use cache"` and PPR directives.

## Phase 3: Construct Pages and Layouts (Next.js 16 Constraints)

1. Create `page.tsx` and if applicable `layout.tsx`.
2. Await your `params` and `searchParams` meticulously—this is critical in Next 16.
3. Set up `<Suspense fallback={...}>` to prevent network waterfalls and stream payloads as soon as possible. Use `next/image` to prevent CLS.

## Phase 4: Construct Interactive Elements (React 19 Constraints)

1. Build Client Components for all buttons, maps, real-time analytics toggles, etc.
2. Implement `<form action={myServerAction}>`. Wrap states with `useActionState` and buttons with `useFormStatus`. 
3. If necessary for UI responsiveness, sprinkle `useOptimistic`.
4. Drop `forwardRef` usage completely—pass `ref` natively.

## Phase 5: Verification (Vercel Core vitals)

1. Check for nested `cat` commands or generic shell tools, prevent that.
2. Run `npm run lint` and `npx tsc --noEmit` locally. 
3. Verify that your UI adheres to the project's styling and layout rules. Dark-mode focus, strictly. 

*Completion*: Present the fully integrated feature and optionally instruct the user on how the Server Component architecture is saving them main-thread payload size!
