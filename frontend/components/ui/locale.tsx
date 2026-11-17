"use client";

import { createContext, useContext } from "react";

/** UI chrome strings for shared components. Learner pages use the Indonesian default; the admin is English. */
const STRINGS = {
  id: { close: "Tutup", loading: "Memuat" },
  en: { close: "Close", loading: "Loading" },
} as const;

export type UiLocale = keyof typeof STRINGS;
const UiLocaleContext = createContext<UiLocale>("id");

export const UiLocaleProvider = UiLocaleContext.Provider;
export const useUiStrings = () => STRINGS[useContext(UiLocaleContext)];
