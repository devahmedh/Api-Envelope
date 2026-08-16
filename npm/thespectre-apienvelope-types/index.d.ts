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

/** A successful response. `result` is non-null and `errorCode` is null. */
export interface ApiSuccess<T> {
  readonly isSuccess: true;
  readonly statusCode: number;
  readonly result: T;
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
  readonly result: null;
  readonly errorCode: string;
  readonly message?: string;
  readonly correlationId: string;
  readonly details?: readonly ErrorDetail[];
}

/**
 * The unified response envelope.
 *
 * Narrow on `isSuccess` to get `result` as `T` and `errorCode` as `string` without assertions:
 *
 * ```ts
 * if (res.isSuccess) {
 *   this.projects = res.result;
 * } else {
 *   this.error = translateError(res.errorCode, lang);
 * }
 * ```
 */
export type ApiResponse<T> = ApiSuccess<T> | ApiFailure;

/**
 * Page metadata accompanying a {@link PagedResult}.
 *
 * The derived members (`pageCount`, `firstRowOnPage`, `lastRowOnPage`) are sent alongside
 * the three raw values so the client never has to recompute them.
 */
export interface PaginationData {
  readonly currentPage: number;
  readonly pageSize: number;
  readonly rowCount: number;
  readonly pageCount: number;
  readonly firstRowOnPage: number;
  readonly lastRowOnPage: number;
}

/**
 * A single page of results together with its page metadata. Placed inside the envelope's
 * `result` slot, so a paged response reads as `result.data` and `result.pagination`.
 */
export interface PagedResult<T> {
  readonly data: readonly T[];
  readonly pagination: PaginationData;
}
