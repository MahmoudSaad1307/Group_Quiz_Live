import { test, expect } from "@playwright/test";

for (const locale of ["en", "ar"] as const) {
  test(`full quiz flow (${locale})`, async ({ browser }) => {
    const adminCtx = await browser.newContext();
    const adminPage = await adminCtx.newPage();

    await adminPage.goto(`/${locale}/admin/login`);
    if (locale === "ar") {
      await expect(adminPage.locator("html")).toHaveAttribute("dir", "rtl");
    }

    await adminPage.fill('input[type="email"]', "admin@quizlive.local");
    await adminPage.fill('input[type="password"]', "ChangeMe123!");
    await adminPage.click('button[type="submit"]');
    await adminPage.waitForURL(`**/${locale}/admin**`);

    await adminPage.goto(`/${locale}/admin/quizzes`);
    await expect(adminPage.locator("h1")).toBeVisible();

    await adminCtx.close();
  });
}
