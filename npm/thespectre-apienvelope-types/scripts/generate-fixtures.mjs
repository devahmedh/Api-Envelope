import { readdirSync, readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { join, dirname, basename } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const goldenDir = join(here, '..', '..', '..', 'tests', 'golden');
const outFile = join(here, '..', 'test', 'fixtures.generated.ts');

const toIdentifier = (fileName) =>
  basename(fileName, '.json').replace(/-([a-z0-9])/g, (_, c) => c.toUpperCase());

const files = readdirSync(goldenDir).filter((f) => f.endsWith('.json')).sort();

if (files.length === 0) {
  throw new Error(`No golden files found in ${goldenDir}`);
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
