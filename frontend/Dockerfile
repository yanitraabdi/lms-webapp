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
