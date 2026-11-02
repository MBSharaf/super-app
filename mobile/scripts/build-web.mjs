/**
 * Copies the site (the same files GitHub Pages serves) into mobile/www for Capacitor.
 * Only one change is made to the copy: <base href> becomes "/" because the app serves the site at its root.
 * Backend, docs, tests and tools are left out.
 */
import { cpSync, rmSync, mkdirSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..', '..');
const out = join(here, '..', 'www');

const ENTRIES = ['index.html', 'offline.html', 'manifest.json', 'og-image.png', 'css', 'js', 'vendor', 'models', 'icons'];

rmSync(out, { recursive: true, force: true });
mkdirSync(out, { recursive: true });
for (const entry of ENTRIES) {
  const from = join(root, entry);
  if (!existsSync(from)) throw new Error(`Missing ${entry}`);
  cpSync(from, join(out, entry), { recursive: true });
}

const indexPath = join(out, 'index.html');
const html = readFileSync(indexPath, 'utf8');
const fixed = html.replace(/<base href="[^"]*"\s*\/?>/, '<base href="/" />');
if (fixed === html) throw new Error('index.html: <base href> not found');
writeFileSync(indexPath, fixed);
console.log(`Site copied to ${out}`);
