#!/usr/bin/env node
/**
 * Production-like static server for performance tests (Node, no packages).
 * It copies how GitHub Pages serves the site, so JMeter results are close to the real demo site:
 *   - the site lives in /super-app/
 *   - text files are compressed (gzip; Brotli too with --br, like our API), every file has an ETag and Last-Modified, and
 *     "Cache-Control: max-age=600" (GitHub Pages' default); conditional requests answer 304
 *   - a path with no file gets 404.html with HTTP 404 (GitHub Pages behaviour for clean URLs)
 * Files and their gzip copies are kept in memory, as a CDN edge would.
 *
 * Usage: node tools/perf/serve-static.js [port=8080] [root=repo folder] [--br]
 */
'use strict';
const http = require('http');
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');
const crypto = require('crypto');

const args = process.argv.slice(2).filter((a) => a !== '--br');
const BROTLI = process.argv.includes('--br');
const PORT = Number(args[0] || 8080);
const ROOT = path.resolve(args[1] || path.join(__dirname, '..', '..'));
const BASE = '/super-app/';
const MIME = {
  '.html': 'text/html; charset=utf-8', '.css': 'text/css; charset=utf-8', '.js': 'application/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8', '.xml': 'application/xml', '.txt': 'text/plain; charset=utf-8',
  '.svg': 'image/svg+xml', '.png': 'image/png', '.jpg': 'image/jpeg', '.webp': 'image/webp', '.ico': 'image/x-icon',
  '.woff2': 'font/woff2', '.glb': 'model/gltf-binary',
};
const COMPRESSIBLE = new Set(['.html', '.css', '.js', '.json', '.xml', '.txt', '.svg']);
const cache = new Map();

/** Reads a file once and keeps it (with its gzip copy) in memory. */
function load(file) {
  if (cache.has(file)) return cache.get(file);
  let entry = null;
  try {
    const stat = fs.statSync(file);
    if (stat.isFile()) {
      const body = fs.readFileSync(file);
      const ext = path.extname(file).toLowerCase();
      entry = {
        body, ext, type: MIME[ext] || 'application/octet-stream',
        gz: COMPRESSIBLE.has(ext) ? zlib.gzipSync(body, { level: 6 }) : null,
        br: BROTLI && COMPRESSIBLE.has(ext) ? zlib.brotliCompressSync(body) : null,
        etag: `"${crypto.createHash('sha1').update(body).digest('hex').slice(0, 16)}"`,
        modified: stat.mtime.toUTCString(),
      };
    }
  } catch { /* missing file */ }
  cache.set(file, entry);
  return entry;
}

/** Maps a URL path to a file inside ROOT, or null when it is outside the site. */
function resolve(urlPath) {
  let rel = decodeURIComponent(urlPath.slice(BASE.length));
  if (rel === '' || rel.endsWith('/')) rel += 'index.html';
  const file = path.normalize(path.join(ROOT, rel));
  return file.startsWith(ROOT) ? file : null;
}

function send(req, res, status, entry) {
  const headers = {
    'Content-Type': entry.type, 'Cache-Control': 'max-age=600', ETag: entry.etag,
    'Last-Modified': entry.modified, Vary: 'Accept-Encoding',
  };
  if (status === 200 && (req.headers['if-none-match'] === entry.etag || req.headers['if-modified-since'] === entry.modified)) {
    res.writeHead(304, headers);
    return res.end();
  }
  const accept = req.headers['accept-encoding'] || '';
  const enc = entry.br && /\bbr\b/.test(accept) ? 'br' : entry.gz && /\bgzip\b/.test(accept) ? 'gzip' : null;
  const body = enc === 'br' ? entry.br : enc === 'gzip' ? entry.gz : entry.body;
  if (enc) headers['Content-Encoding'] = enc;
  headers['Content-Length'] = body.length;
  res.writeHead(status, headers);
  res.end(req.method === 'HEAD' ? undefined : body);
}

const server = http.createServer((req, res) => {
  const urlPath = (req.url || '/').split('?')[0];
  if (urlPath === '/' || urlPath === '/super-app') { res.writeHead(301, { Location: BASE }); return res.end(); }
  if (!urlPath.startsWith(BASE)) { res.writeHead(404); return res.end('Not found'); }
  const file = resolve(urlPath);
  const entry = file && load(file);
  if (entry) return send(req, res, 200, entry);
  return send(req, res, 404, load(path.join(ROOT, '404.html')));
});
server.keepAliveTimeout = 65000;
server.listen(PORT, () => console.log(`Super App (perf server) at http://localhost:${PORT}${BASE}  root=${ROOT}`));
