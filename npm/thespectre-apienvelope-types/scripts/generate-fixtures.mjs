import { readdirSync, readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { join, dirname, basename } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const goldenDir = join(here, '..', '..', '..', 'tests', 'golden');
const outFile = join(here, '..', 'test', 'fixtures.generated.ts');

const toIdentifier = (fileName) =>
  basename(fileName, '.json').replace(/-([a-z0-9])/g, (_, c) => c.toUpperCase());

const EXPECTED = [
  'error-409.json',
  'error-500-development.json',
  'error-500-production.json',
  'notfound-404.json',
  'success-200.json',
  'success-204.json',
  'success-paged-200.json',
  'unauthorized-401.json',
  'validation-400.json',
];

const files = readdirSync(goldenDir).filter((f) => f.endsWith('.json')).sort();

const missing = EXPECTED.filter((f) => !files.includes(f));
const unexpected = files.filter((f) => !EXPECTED.includes(f));

if (missing.length > 0 || unexpected.length > 0) {
  throw new Error(
    `Golden set mismatch in ${goldenDir}.` +
      (missing.length ? ` Missing: ${missing.join(', ')}.` : '') +
      (unexpected.length ? ` Unexpected: ${unexpected.join(', ')}.` : '') +
      ' Adding or removing a golden is a wire-contract change: update EXPECTED deliberately.',
  );
}

const body = files
  .map((file) => {
    const json = JSON.parse(readFileSync(join(goldenDir, file), 'utf8'));
    return `export const ${toIdentifier(file)} = ${JSON.stringify(json, null, 2)} as const;`;
  })
  .join('\n\n');

mkdirSync(dirname(outFile), { recursive: true });
writeFileSync(
  outFile,
  `// GENERATED FROM tests/golden/*.json BY scripts/generate-fixtures.mjs — DO NOT EDIT.\n\n${body}\n`,
  'utf8',
);

console.log(`Generated ${files.length} fixtures into ${outFile}`);
