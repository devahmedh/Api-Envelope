import type { ApiResponse, ApiFailure, ApiSuccess, ErrorDetail } from '../index';
import {
  success200,
  success204,
  error409,
  error500Production,
  error500Development,
  validation400,
  unauthorized401,
  notfound404,
} from './fixtures.generated';

interface Sample {
  id: number;
  name: string;
}

// Every golden file must be assignable to the contract.
success200 satisfies ApiSuccess<Sample>;
success204 satisfies ApiSuccess<null>;
error409 satisfies ApiFailure;
error500Production satisfies ApiFailure;
error500Development satisfies ApiFailure;
validation400 satisfies ApiFailure;
unauthorized401 satisfies ApiFailure;
notfound404 satisfies ApiFailure;

// `satisfies` above pins the LOWER bound: every required property is present and correctly
// typed. It cannot catch an ADDED property, because excess-property checking only applies to
// fresh object literals, not to identifiers. These assertions pin the upper bound: a new key
// on the wire fails to compile here.
type KeysSubsetOf<TActual, TExpected> = keyof TActual extends keyof TExpected ? true : never;

export const _keys200: KeysSubsetOf<typeof success200, ApiSuccess<Sample>> = true;
export const _keys204: KeysSubsetOf<typeof success204, ApiSuccess<null>> = true;
export const _keys409: KeysSubsetOf<typeof error409, ApiFailure> = true;
export const _keys500p: KeysSubsetOf<typeof error500Production, ApiFailure> = true;
export const _keys500d: KeysSubsetOf<typeof error500Development, ApiFailure> = true;
export const _keys400: KeysSubsetOf<typeof validation400, ApiFailure> = true;
export const _keys401: KeysSubsetOf<typeof unauthorized401, ApiFailure> = true;
export const _keys404: KeysSubsetOf<typeof notfound404, ApiFailure> = true;

// The union must narrow on isSuccess without assertions.
function consume(response: ApiResponse<Sample>): string {
  if (response.isSuccess) {
    const name: string = response.data.name;
    return name;
  }

  const code: string = response.errorCode;
  const details: readonly ErrorDetail[] = response.details ?? [];
  return `${code}:${details.length}`;
}

consume(success200);

// A success must not be allowed to carry an errorCode.
export const invalidSuccess: ApiSuccess<Sample> = {
  isSuccess: true,
  statusCode: 200,
  data: { id: 1, name: 'x' },
  // @ts-expect-error errorCode must be null on ApiSuccess
  errorCode: 'SOMETHING',
  correlationId: 'c',
};

// A failure must not be allowed to carry data.
export const invalidFailure: ApiFailure = {
  isSuccess: false,
  statusCode: 400,
  // @ts-expect-error data must be null on ApiFailure
  data: { id: 1 },
  errorCode: 'BAD_REQUEST',
  correlationId: 'c',
};
