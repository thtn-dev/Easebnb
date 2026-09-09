import type { z } from "zod";

/** Maps a ZodError to { fieldName: [{ message }] } for FieldError props. */
export function collectFieldErrors(
  error: z.ZodError,
): Record<string, Array<{ message?: string }>> {
  const errors: Record<string, Array<{ message?: string }>> = {};
  for (const issue of error.issues) {
    const key = issue.path[0];
    if (typeof key === "string") {
      (errors[key] ??= []).push({ message: issue.message });
    }
  }
  return errors;
}
