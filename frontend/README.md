# LMS Frontend (M0 skeleton)

Next.js (App Router, TypeScript) + Tailwind CSS v4 frontend skeleton for the LMS web app.

This is **M0**: skeleton, design-system tokens, route tree, and wiring only.
No product screens, no business logic, no real API calls.

## Stack

- **Next.js** (App Router) + **React 19** + **TypeScript**
- **Tailwind CSS v4** (CSS-first config via `@theme` in `app/globals.css`)
- **Plus Jakarta Sans** self-hosted via `next/font/google`
- **TanStack Query** (`app/providers.tsx`)
- **next-intl** (default locale `id`, messages in `messages/id.json`)
- **driver.js** (dependency only, no tour yet)

## Design tokens

All Design Foundation tokens live in `app/globals.css`, mapped two ways:

1. Tailwind v4 `@theme` block → utilities like `bg-bg`, `text-ink`, `bg-primary`, `rounded-base`, `shadow-sm`.
2. Raw `--ds-*` custom properties on `:root` → reference directly as `var(--ds-color-primary)`.

Light mode only.

## Scripts

```bash
npm run dev          # dev server
npm run build        # production build
npm run start        # serve production build
npm run lint         # eslint
npm run generate:api # regenerate api-client/schema.ts from backend OpenAPI (needs backend on :8080)
```

## Routes (rendering modes)

| Route | Group | Mode |
| --- | --- | --- |
| `/` | (marketing) | SSG |
| `/pricing`, `/how-it-works`, `/for-business`, `/about` | (marketing) | SSG |
| `/legal/[doc]` | (marketing) | SSG (dynamic param) |
| `/catalog`, `/catalog/[level]`, `/modules/[slug]` | (catalog) | ISR |
| `/verify/[code]` | — | SSR (force-dynamic) |
| `/app/dashboard`, `/app/learn/[id]`, `/app/account`, `/app/certificates` | app | CSR |
| `/admin` | — | CSR (role-gated placeholder) |

## Docker

Multi-stage `Dockerfile` (Node 22 alpine) builds and serves via Next.js `standalone` output. Exposes port 3000.
