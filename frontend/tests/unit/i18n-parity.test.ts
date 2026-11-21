import { describe, it, expect } from "vitest";
import en from "../../messages/en.json";
import ar from "../../messages/ar.json";

function collectKeys(obj: Record<string, unknown>, prefix = ""): string[] {
  return Object.entries(obj).flatMap(([key, value]) => {
    const full = prefix ? `${prefix}.${key}` : key;
    if (value && typeof value === "object" && !Array.isArray(value)) {
      return collectKeys(value as Record<string, unknown>, full);
    }
    return [full];
  });
}

describe("i18n message parity", () => {
  it("en.json and ar.json have identical translation keys", () => {
    const enKeys = collectKeys(en).sort();
    const arKeys = collectKeys(ar).sort();
    expect(arKeys).toEqual(enKeys);
  });
});
