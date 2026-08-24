# Easebnb Web

Next.js frontend for the Easebnb platform. Built on Next.js 16 (App Router,
Turbopack), React 19, TypeScript (strict), Tailwind CSS v4 and shadcn/ui
(Base UI). This is a **foundation** — infrastructure is complete, business
features are added per-domain.

## Commands

| Command             | Description                          |
| ------------------- | ------------------------------------ |
| `npm run dev`       | Start the dev server (Turbopack)     |
| `npm run build`     | Production build                     |
| `npm run start`     | Serve the production build           |
| `npm run lint`      | ESLint (flat config)                 |
| `npm run typecheck` | `tsc --noEmit`                       |

## Folder structure

```
web/
├── public/                  # Static assets
├── src/
│   ├── app/                 # Routes only (layout, pages, loading, error, not-found)
│   ├── components/
│   │   └── ui/              # shadcn/ui primitives (generated, don't rewrite)
│   ├── features/            # Domain modules — the home of business code
│   │   └── health/          # Example feature (delete when real features exist)
│   │       ├── api.ts       # API service functions (only place that calls `api`)
│   │       ├── queries.ts   # Query key factory + queryOptions / mutations
│   │       ├── schemas.ts   # Zod schemas; types derived via z.infer
│   │       └── components/  # Domain components
│   ├── hooks/               # Cross-cutting React hooks
│   ├── lib/
│   │   ├── api/             # HTTP client layer (client/config/errors/types)
│   │   ├── env.ts           # Zod-validated NEXT_PUBLIC_* configuration
│   │   ├── query-client.ts  # QueryClient factory + getQueryClient (SSR-safe)
│   │   └── utils.ts         # cn() helper
│   ├── providers/           # Global client providers (query-provider)
│   └── stores/              # Zustand stores (client-only UI state)
├── .env.example             # Documented environment variables
└── next.config.ts           # /api rewrite proxy (see Environment)
```

## Architecture

```
UI (Server or Client Component)
 ↓
useQuery / useMutation          — TanStack Query (server state)
 ↓
features/<domain>/queries.ts    — query key factory + options
 ↓
features/<domain>/api.ts        — feature API service (+ Zod validation)
 ↓
lib/api/client.ts              — fetch wrapper: base URL, timeout, errors
 ↓
Backend API (.NET)
```

Rules of thumb:

- **Server Components by default**; `"use client"` only where interactivity,
  browser APIs or hooks require it. Data fetching in Server Components can
  call the feature `api.ts` service directly.
- Components never call `fetch()` or build URLs — always through the
  feature's API service.
- Errors from the API are always `ApiError` (`lib/api/errors.ts`) with a
  `kind` (http/timeout/network/unknown), optional `status`, `code`,
  `details`, `requestId`. Use `getErrorMessage(error)` in the UI so
  internals never leak.

## State management

| State                             | Where it lives                          |
| --------------------------------- | --------------------------------------- |
| Server data (API responses)       | TanStack Query (`features/*/queries.ts`) |
| Client UI state (drawers, wizard) | Zustand (`stores/*-store.ts`)           |
| Local component state             | `useState` / `useReducer`               |
| Form state + validation           | React state + Zod (`features/*/schemas.ts`) |

Never put API responses in Zustand — TanStack Query already owns caching,
retries, and revalidation. Stores are per-domain files, not a mega-store
(`stores/ui-store.ts` is the convention example).

## Adding a new API

1. **Schema** — `features/<domain>/schemas.ts`: define the request/response
   Zod schema and derive types with `z.infer`.
2. **Service** — `features/<domain>/api.ts`: add a function that calls
   `api.get<T>("/path")` (or post/put/patch/delete) and validates the
   response with the schema.
3. **Query/Mutation** — `features/<domain>/queries.ts`: add a key to the
   factory and a `queryOptions(...)` (or `useMutation` wrapper).
4. **Consume** — Client Components use `useQuery(featureQuery)`; Server
   Components call the service function directly (or prefetch +
   `HydrationBoundary` when hydration is needed).

`api.get<TResponse>("/users")`, `api.post<TResponse, TBody>("/users", body)`,
`api.patch<TResponse, TBody>("/users/:id", body)` — query params, headers,
timeout and `AbortSignal` are supported via the options argument.

## Adding a new feature

Create `features/<domain>/` with `api.ts`, `queries.ts`, `schemas.ts`,
`components/` — see `features/health` for a working example. Shared,
non-domain components go in `components/` (shadcn primitives stay in
`components/ui/`).

## Environment

Copy `.env.example` to `.env.local` and fill in:

| Variable              | Scope   | Description                                                                             |
| --------------------- | ------- | --------------------------------------------------------------------------------------- |
| `NEXT_PUBLIC_API_URL` | Browser | Base URL seen from the browser. Empty = same-origin `/api` (default, recommended).      |
| `API_PROXY_URL`       | Server  | Backend origin (e.g. `http://localhost:7000`). Enables the `/api/*` rewrite proxy + SSR. |

Recommended local setup: leave `NEXT_PUBLIC_API_URL` empty and set
`API_PROXY_URL` — the browser calls same-origin `/api/*`, Next.js forwards
to the backend (no CORS, cookies just work), and server rendering calls the
backend directly. `API_PROXY_URL` is never exposed to the browser.

## SSR with TanStack Query

`lib/query-client.ts` follows the canonical App Router pattern: a fresh
`QueryClient` per server render (never shared across requests) and a
singleton in the browser. To SSR a query: in a Server Component, `const
queryClient = getQueryClient()`, `queryClient.prefetchQuery(healthQuery)`,
then wrap the consumer in `<HydrationBoundary state={dehydrate(queryClient)}>`.
Client-only data just uses `useQuery` — no prefetch needed.

## Authentication

JWT Bearer auth against `/api/v1/auth/*` (see `open-api-v1.json`), built on
the shared API client:

- **State** — `src/stores/auth-store.ts` (Zustand): `user`, `accessToken`,
  `refreshToken`, `isAuthenticated`, `isLoading`. UI uses semantic actions
  from `src/features/auth/session.ts` (`login`, `register`, `logout`,
  `revokeToken`) — no raw token handling in components.
- **Storage** — `rememberMe: false` → `sessionStorage` (dies with the tab);
  `rememberMe: true` → `localStorage` (persistent). Persisted data is
  re-validated with Zod on hydration. Tokens are never rendered in the UI.
- **Authorized requests** — `lib/api/client.ts` attaches
  `Authorization: <tokenType> <accessToken>` automatically via the auth
  bridge (`lib/api/auth-bridge.ts`). "Bearer" is set in one place from the
  login response.
- **Auto refresh** — on a 401 the client calls the single-flight
  `refreshAuthSession()` (concurrent 401s share one refresh request), then
  retries the original request exactly once. Refresh failure clears the
  session; guards redirect to `/login`. Auth endpoints themselves are
  `skipAuth`, so a refresh loop is impossible.
- **Routes** — `/login`, `/register` (GuestGuard: signed-in users are
  redirected to `/dashboard`), `/dashboard` (AuthGuard). Guards wait for
  hydration (`isLoading`) before redirecting.
- **Logout** — always clears the local session, even when the backend
  revocation call fails.

Local backend: set `API_PROXY_URL=http://localhost:5282` in `.env.local`
(see `.env.example`).

## Conventions

- Imports use the `@/*` alias (maps to `src/*`).
- Next.js 16: `params`/`searchParams` are Promises (`await` them);
  `error.tsx` receives `retry()`; the proxy file (renamed from middleware)
  is `proxy.ts` if ever needed.
- Route-level `loading.tsx` / `error.tsx` / `not-found.tsx` can be added per
  segment; root-level baselines already exist.
