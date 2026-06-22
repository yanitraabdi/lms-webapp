# api-client

Typed TypeScript bindings **generated** from the backend's OpenAPI specification.
Nothing in this directory is written by hand (except this README).

## How it's generated

The npm script in `package.json`:

```json
"generate:api": "openapi-typescript http://localhost:8080/openapi/v1.json -o api-client/schema.ts"
```

Run it (once the backend dev server is up on port 8080):

```bash
npm run generate:api
```

This fetches the live OpenAPI document at `http://localhost:8080/openapi/v1.json`
and overwrites `api-client/schema.ts` with the generated types.

## Placeholder

Until the backend exists, `schema.ts` contains a minimal committed placeholder:

```ts
export type Health = { status: string };
```

This keeps `npm run build` / `npm run lint` green. The placeholder is replaced
the first time `generate:api` runs against a real backend.

## Consuming the types

Import generated types through `lib/api.ts` (the single stable entry point) rather
than reaching into this directory directly, e.g.:

```ts
import type { Health } from "@/lib/api";
```
