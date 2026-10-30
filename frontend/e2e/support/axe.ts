import AxeBuilder from "@axe-core/playwright";
import { expect, type Page } from "@playwright/test";

/** Rules switched off on purpose. Every entry needs a reason; an empty list is the goal. */
export const DISABLED_RULES: Array<{ id: string; reason: string }> = [];

export async function expectNoSeriousA11y(page: Page) {
  const { violations } = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
    .disableRules(DISABLED_RULES.map((r) => r.id))
    .analyze();
  const bad = violations.filter((v) => v.impact === "serious" || v.impact === "critical");
  expect(bad.map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(" ").slice(-70) + (process.env.AXE_DEBUG ? " <<" + n.html.slice(0, 140) + ">> " + (n.any[0]?.message ?? "") : "")).join(" | ")}`)).toEqual([]);
}
