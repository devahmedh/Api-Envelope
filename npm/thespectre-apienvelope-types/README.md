# thespectre-apienvelope-types

TypeScript contract for the [`TheSpectre.ApiEnvelope`](https://github.com/TheSpectreDev/Api-Envelope)
unified API response envelope.

```ts
import type { ApiResponse } from 'thespectre-apienvelope-types';

const res: ApiResponse<Project[]> = await http.get('/api/projects').toPromise();

if (res.isSuccess) {
  this.projects = res.data;            // Project[] — narrowed, no assertion
} else {
  this.error = translateError(res.errorCode, lang);   // string — never render res.message
}
```

`message` is diagnostics only. Never render it to a user — translate `errorCode` instead.

These types are verified in CI against the same golden JSON files the .NET tests assert on,
so they cannot drift from what the server actually emits.

MIT.
