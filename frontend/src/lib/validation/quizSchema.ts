import { z } from "zod";

export const allowedPoints = [0, 500, 1000, 2000] as const;

export const optionSchema = z.object({
  text: z.string().trim().min(1, "Option text is required").max(120, "Max 120 characters"),
  isCorrect: z.boolean(),
});

export const questionSchema = z
  .object({
    type: z.enum(["SingleChoice", "TrueFalse"]),
    text: z.string().trim().min(1, "Question text is required").max(500, "Max 500 characters"),
    imageUrl: z.string().max(500).nullable().optional(),
    explanation: z.string().max(500).nullable().optional(),
    timeLimitSeconds: z.number().int().min(5).max(120),
    points: z.number().int().refine((v) => (allowedPoints as readonly number[]).includes(v), {
      message: "Points must be 0, 500, 1000, or 2000",
    }),
    options: z.array(optionSchema),
  })
  .superRefine((val, ctx) => {
    if (val.type === "TrueFalse" && val.options.length !== 2) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "True/False must have exactly 2 options",
        path: ["options"],
      });
    }
    if (val.type === "SingleChoice" && (val.options.length < 2 || val.options.length > 4)) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "Multiple choice must have 2 to 4 options",
        path: ["options"],
      });
    }
    const correctCount = val.options.filter((o) => o.isCorrect).length;
    if (correctCount !== 1) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "Exactly 1 option must be marked as correct",
        path: ["options"],
      });
    }
  });

export const quizMetadataSchema = z.object({
  title: z.string().trim().min(1, "Title is required").max(120, "Max 120 characters"),
  description: z.string().max(500).nullable().optional(),
  language: z.enum(["En", "Ar"]),
});
