import Link from "next/link";

export default function NotFound() {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-bg px-6 text-center">
      <span className="text-6xl font-extrabold tracking-tight text-primary">404</span>
      <h1 className="text-2xl font-extrabold">Halaman tidak ditemukan</h1>
      <p className="max-w-sm text-sm text-ink-muted">
        Maaf, halaman yang Anda cari tidak ada atau telah dipindahkan.
      </p>
      <div className="mt-2 flex gap-2.5">
        <Link href="/" className="rounded-sm bg-primary px-5 py-2.5 text-sm font-bold text-primary-ink hover:bg-primary-hover">
          Ke beranda
        </Link>
        <Link href="/catalog" className="rounded-sm border border-border bg-surface px-5 py-2.5 text-sm font-bold text-ink hover:bg-surface-2">
          Jelajahi katalog
        </Link>
      </div>
    </div>
  );
}
