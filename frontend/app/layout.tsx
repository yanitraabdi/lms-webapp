import type { Metadata } from "next";
import { Plus_Jakarta_Sans } from "next/font/google";
import { NextIntlClientProvider } from "next-intl";
import { getLocale, getMessages } from "next-intl/server";
import { Providers } from "./providers";
import "./globals.css";

// Self-hosted via next/font/google (no <link> to Google Fonts). Exposed as the
// CSS variable consumed by globals.css / the Tailwind --font-sans token.
const plusJakarta = Plus_Jakarta_Sans({
  subsets: ["latin"],
  weight: ["400", "500", "600", "700", "800"],
  variable: "--font-plus-jakarta",
  display: "swap",
});

export const metadata: Metadata = {
  // Was "LMS Akademi" — the archived product's name, shown in every browser tab beside the
  // INVERTA favicon. The template lets a page set its own title without repeating the brand.
  title: {
    default: "INVERTA — Persiapan TOEFL",
    template: "%s — INVERTA",
  },
  description:
    "Program persiapan TOEFL terstruktur dalam Bahasa Indonesia: video, tes per sesi, sesi live, " +
    "dan simulasi TOEFL ITP lengkap dengan sertifikat prediksi skor.",
};

export default async function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  // M0: single locale (`id`). NextIntlClientProvider hydrates messages for any
  // client components that use translations.
  const locale = await getLocale();
  const messages = await getMessages();

  return (
    <html lang={locale} className={plusJakarta.variable}>
      <body>
        <NextIntlClientProvider locale={locale} messages={messages}>
          <Providers>{children}</Providers>
        </NextIntlClientProvider>
      </body>
    </html>
  );
}
