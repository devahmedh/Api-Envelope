/**
 * A single field-level failure.
 *
 * `errorCode` is a stable key; `params` carries the structured values the client
 * substitutes into its own translated string, so the server never composes user-facing text.
 */
export interface ErrorDetail {
  readonly field: string;
  readonly errorCode: string;
  readonly params?: Readonly<Record<string, string | number | boolean | null>>;
}

/** A successful response. `data` is non-null and `errorCode` is null. */
export interface ApiSuccess<T> {
  readonly isSuccess: true;
  readonly statusCode: number;
  readonly data: T;
  readonly errorCode: null;
  readonly correlationId: string;
}

/**
 * A failed response. `errorCode` is the key to switch on.
 *
 * `message` is diagnostics only — never render it to a user.
 */
export interface ApiFailure {
  readonly isSuccess: false;
  readonly statusCode: number;
  readonly data: null;
  readonly errorCode: string;
  readonly message?: string;
  readonly correlationId: string;
  readonly details?: readonly ErrorDetail[];
}

/**
 * The unified response envelope.
 *
 * Narrow on `isSuccess` to get `data` as `T` and `errorCode` as `string` without assertions:
 *
 * ```ts
 * if (res.isSuccess) {
 *   this.projects = res.data;
 * } else {
 *   this.error = translateError(res.errorCode, lang);
 * }
 * ```
 */
export type ApiResponse<T> = ApiSuccess<T> | ApiFailure;
