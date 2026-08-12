// GENERATED FROM tests/golden/*.json BY scripts/generate-fixtures.mjs — DO NOT EDIT.

export const error409 = {
  "isSuccess": false,
  "statusCode": 409,
  "data": null,
  "errorCode": "PROJECT_CODE_TAKEN",
  "message": "Project code PERMIT already exists in this tenant.",
  "correlationId": "4bf92f3577b34da6a3ce929d0e0e4736"
} as const;

export const error500Development = {
  "isSuccess": false,
  "statusCode": 500,
  "data": null,
  "errorCode": "INTERNAL_ERROR",
  "message": "Object reference not set to an instance of an object.",
  "correlationId": "4bf92f3577b34da6a3ce929d0e0e4736"
} as const;

export const error500Production = {
  "isSuccess": false,
  "statusCode": 500,
  "data": null,
  "errorCode": "INTERNAL_ERROR",
  "correlationId": "4bf92f3577b34da6a3ce929d0e0e4736"
} as const;

export const notfound404 = {
  "isSuccess": false,
  "statusCode": 404,
  "data": null,
  "errorCode": "NOT_FOUND",
  "correlationId": "4bf92f3577b34da6a3ce929d0e0e4736"
} as const;

export const success200 = {
  "isSuccess": true,
  "statusCode": 200,
  "data": {
    "id": 42,
    "name": "Ahmed"
  },
  "errorCode": null,
  "correlationId": "4bf92f3577b34da6a3ce929d0e0e4736"
} as const;

export const success204 = {
  "isSuccess": true,
  "statusCode": 204,
  "data": null,
  "errorCode": null,
  "correlationId": "4bf92f3577b34da6a3ce929d0e0e4736"
} as const;

export const unauthorized401 = {
  "isSuccess": false,
  "statusCode": 401,
  "data": null,
  "errorCode": "UNAUTHORIZED",
  "correlationId": "4bf92f3577b34da6a3ce929d0e0e4736"
} as const;

export const validation400 = {
  "isSuccess": false,
  "statusCode": 400,
  "data": null,
  "errorCode": "VALIDATION_FAILED",
  "correlationId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "details": [
    {
      "field": "title",
      "errorCode": "TOO_LONG",
      "params": {
        "max": 400
      }
    },
    {
      "field": "code",
      "errorCode": "REQUIRED"
    }
  ]
} as const;
