/**
 * Minimal placeholder used by every M0 route stub. Renders the route's name and
 * its intended rendering mode — just enough to prove the route tree compiles.
 * Not part of the design-system kit.
 */
export function RouteStub({
  name,
  mode,
}: {
  name: string;
  mode: string;
}) {
  return (
    <main className="mx-auto max-w-3xl px-6 py-16">
      <h1 className="text-2xl font-bold text-ink">{name}</h1>
      <p className="mt-2 text-ink-muted">Rendering mode: {mode}</p>
    </main>
  );
}
