import { HttpErrorResponse } from '@angular/common/http';
import { Problem } from './api.types';

export interface ErrorMessage {
  /** One line for the user. */
  message: string;
  /** Per-field validation messages, when the server sent them. */
  fields: string[];
  traceId?: string;
}

/**
 * Turns whatever went wrong into something to show. The API answers every failure with an RFC 7807 problem
 * (title, detail, a machine-readable code and, for validation, a field map), so show those instead of "HTTP 409".
 */
export function describeError(error: unknown): ErrorMessage {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return { message: 'Cannot reach the server. Check that the API is running.', fields: [] };
    }

    const problem = error.error as Problem | null;
    if (problem && typeof problem === 'object' && (problem.title || problem.detail)) {
      const fields = Object.entries(problem.errors ?? {}).flatMap(([field, messages]) => messages.map((m) => `${field}: ${m}`));
      return { message: problem.detail ?? problem.title ?? 'The request failed.', fields, traceId: problem.traceId };
    }

    return { message: `The request failed (HTTP ${error.status}).`, fields: [] };
  }

  return { message: error instanceof Error ? error.message : 'Something went wrong.', fields: [] };
}
