---
name: modern-react-nextjs
description: Step by step instructions on how to structure UI implementation based on React 19.2 and Next.js 16 standards.
allowed-tools: Read, Write, Edit
---

# Modern React & Next.js Module Skill (19.2 / 16+)

When asked to build a new feature, page, or complex component in this workspace, follow this exact skill protocol to ensure invariant adherence to Vercel and React 19 core guidelines.

## 🛠 Step 1: Establish the Component Boundary

Before coding, mentally separate Server Components (RSC) from Client Components.

- Is the component exclusively fetching data without interactions? **RSC**.
- Does it need `useState`, `onClick`, or `window` APIs? **Client**.
- **Golden Rule**: Never wrap a heavy client component blindly. Push the `"use client"` directive as far down the DOM tree as possible. 

## 🛠 Step 2: Ensure Async Data Integrity (Next.js 16)

When writing `page.tsx` or `layout.tsx`:

```tsx
// This is the correct, mandatory way to read params in Next.js 16+
export default async function Page({ params, searchParams }: { 
  params: Promise<{ id: string }>, 
  searchParams: Promise<{ query: string }> 
}) {
  const { id } = await params;
  const { query } = await searchParams;
  // ...
}
```

## 🛠 Step 3: Implement Caching API

Move away from legacy ISR (`revalidate: 60`). Favor explicitly tagging data resources with Next Cache Directives.

1. Implement `"use cache"` at the function boundary where compute is heavy.
2. Pair fetch functions with `next: { tags: ["my-business-entity"] }`.
3. In Server Actions enforcing data change, ALWAYS trigger `updateTag()` / `revalidateTag()`.

## 🛠 Step 4: Mutate with Modern Form Actions

If creating a form, avoid traditional `onSubmit` with `e.preventDefault()`. Create Server Actions.

1. Define async `action` function in a `"use server"` file or closure.
2. Expose the pending state using React 19's `useActionState`.
3. Add a `SubmitButton` using `useFormStatus` to handle loading automatically.
4. For visually synchronous interactions, inject `useOptimistic` logic.

## 🛠 Step 5: Leverage Tailwind 4 & Vercel Performance

- Every `<img />` tag is a failure; use `<Image />` (`next/image`).
- Every font import via `<link>` is a failure; use `next/font`.
- Always structure Tailwind classnames handling `dark:` to enforce the workspace standard `zinc` / `black` neutral tone palettes natively.

Read `GEMINI.md` for project-specific architecture rules.
