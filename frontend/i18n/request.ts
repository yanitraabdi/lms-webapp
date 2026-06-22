import { getRequestConfig } from "next-intl/server";

// M0 i18n: single locale only (`id` / Bahasa Indonesia), no locale-prefixed
// routing. The plugin (configured in next.config.ts) calls this on every
// request to supply the active locale + its messages.
const DEFAULT_LOCALE = "id";

export default getRequestConfig(async () => {
  const locale = DEFAULT_LOCALE;

  return {
    locale,
    messages: (await import(`../messages/${locale}.json`)).default,
  };
});
