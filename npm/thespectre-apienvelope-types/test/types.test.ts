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
