"use client";

import { useState } from "react";

// Google SSO backend is wired but dormant until OAuth credentials are configured.
// Until then this shows a "coming soon" note rather than a broken redirect.
export function GoogleButton({ label }: { label: string }) {
  const [note, setNote] = useState(false);
  return (
    <div className="flex flex-col gap-2">
      <button
        type="button"
        onClick={() => setNote(true)}
        className="flex w-full items-center justify-center gap-2.5 rounded-sm border-[1.5px] border-border bg-surface px-3 py-3 text-sm font-bold text-ink transition-colors hover:bg-surface-2"
      >
        <svg width="18" height="18" viewBox="0 0 24 24" aria-hidden="true">
          <path fill="#4285F4" d="M22.5 12.3c0-.8-.1-1.5-.2-2.2H12v4.2h5.9a5 5 0 0 1-2.2 3.3v2.7h3.5c2-1.9 3.3-4.7 3.3-8z" />
          <path fill="#34A853" d="M12 23c3 0 5.5-1 7.3-2.7l-3.5-2.7c-1 .7-2.2 1.1-3.8 1.1-2.9 0-5.4-2-6.3-4.6H2v2.8A11 11 0 0 0 12 23z" />
          <path fill="#FBBC05" d="M5.7 14.1a6.6 6.6 0 0 1 0-4.2V7.1H2a11 11 0 0 0 0 9.8z" />
          <path fill="#EA4335" d="M12 5.4c1.6 0 3 .6 4.2 1.7l3.1-3.1A11 11 0 0 0 2 7.1l3.7 2.8C6.6 7.3 9.1 5.4 12 5.4z" />
        </svg>
        {label}
      </button>
      {note && <p className="text-center text-xs text-ink-subtle">Login dengan Google akan segera tersedia.</p>}
    </div>
  );
}
