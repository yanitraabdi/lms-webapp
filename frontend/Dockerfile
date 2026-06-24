# syntax=docker/dockerfile:1

# Multi-stage build for the Next.js frontend, run via `next start` (full node_modules).
# Debian-slim (glibc) base avoids the Alpine/musl native-binding crashes from
# Tailwind v4 (oxide) / lightningcss during `next build`.

# ---- deps ----
FROM node:24-slim AS deps
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm ci

# ---- builder ----
FROM node:24-slim AS builder
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
ENV NEXT_TELEMETRY_DISABLED=1

# Build-time config. NEXT_PUBLIC_* are inlined into the client bundle by `next build`, so the
# browser API base and site URL must be set HERE, not just at runtime.
#  - Default NEXT_PUBLIC_API_BASE_URL=http://localhost:8080 preserves the local dev build (browser
#    talks to the API directly). For a single-hostname/tunnel deploy, pass "" so the browser uses
#    same-origin /api/* and Next proxies to API_INTERNAL_URL (see next.config.ts rewrites).
ARG NEXT_PUBLIC_API_BASE_URL=http://localhost:8080
ARG NEXT_PUBLIC_SITE_URL
ARG API_INTERNAL_URL=http://api:8080
ENV NEXT_PUBLIC_API_BASE_URL=$NEXT_PUBLIC_API_BASE_URL
ENV NEXT_PUBLIC_SITE_URL=$NEXT_PUBLIC_SITE_URL
ENV API_INTERNAL_URL=$API_INTERNAL_URL
RUN npm run build

# ---- runner ----
FROM node:24-slim AS runner
WORKDIR /app
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV PORT=3000
ENV HOSTNAME=0.0.0.0

RUN groupadd --system --gid 1001 nodejs \
  && useradd --system --uid 1001 --gid nodejs nextjs

# Copy the full built app (source + node_modules + .next) so `next start` runs
# exactly as it does locally.
COPY --from=builder --chown=nextjs:nodejs /app ./

USER nextjs
EXPOSE 3000

CMD ["npm", "run", "start"]
