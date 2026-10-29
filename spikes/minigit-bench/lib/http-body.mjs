// Reading a bounded HTTP request body: the one place the 8 MB cap lives (the receiver and the capture's file sink both use it).
import { gunzipSync } from 'node:zlib';
import { fail, ok } from './result.mjs';

export const MAX_BODY_BYTES = 8 * 1024 * 1024;

/** ok(the whole body as a Buffer), `fail('too large')` past the cap (the rest is drained, never buffered), gunzipped if sent so. */
export function readBody(req, limit = MAX_BODY_BYTES) {
  return new Promise(resolve => {
    const chunks = [];
    let size = 0;
    req.on('data', chunk => {
      size += chunk.length;
      if (size <= limit) chunks.push(chunk);
    });
    req.on('error', e => resolve(fail(`request error: ${e.message}`)));
    req.on('end', () => resolve(size > limit ? fail('too large') : inflate(req, Buffer.concat(chunks), limit)));
  });
}

function inflate(req, buffer, limit) {
  if (req.headers['content-encoding'] !== 'gzip') return ok(buffer);
  try {
    return ok(gunzipSync(buffer, { maxOutputLength: limit }));
  } catch (e) {
    return fail(`gzip body unreadable: ${e.message}`);
  }
}

/** True for `application/json` with or without parameters (`; charset=utf-8`). */
export const isJson = contentType => /^application\/json\s*(;|$)/i.test(contentType ?? '');

/** The one line shape both OTLP file writers keep a request in: `{ receivedAt, path, contentType, body }` with the body RAW (never parsed). */
export const rawBodyLine = (req, body, receivedAt = Date.now()) => ({ receivedAt, path: req.url, contentType: req.headers['content-type'] ?? null, body });
