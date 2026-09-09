import { z } from "zod";

/**
 * Example schema — the backend health contract is not final yet, so extra
 * keys are ignored and fields stay optional until the real shape settles.
 * Types are derived from schemas (never duplicated).
 */
export const healthSchema = z.object({
  status: z.string(),
  checks: z
    .array(
      z.object({
        name: z.string(),
        status: z.string(),
      }),
    )
    .optional(),
});

export type Health = z.infer<typeof healthSchema>;
