import { describe, it, expect, vi, beforeEach } from "vitest";
import { ApiError, apiFetch } from "@/lib/api/client";

describe("apiFetch", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("returns parsed JSON on 200 OK", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ id: "1", email: "a@b.c", displayName: "Admin" }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      ),
    );

    const data = await apiFetch<{ id: string }>("/api/auth/me");
    expect(data.id).toBe("1");
  });

  it("throws ApiError with ProblemDetails on 401", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({ title: "Unauthorized", detail: "Bad creds", status: 401 }),
          { status: 401, headers: { "Content-Type": "application/json" } },
        ),
      ),
    );

    await expect(apiFetch("/api/auth/me")).rejects.toMatchObject({
      status: 401,
      message: "Bad creds",
    });
    expect(ApiError).toBeDefined();
  });
});
