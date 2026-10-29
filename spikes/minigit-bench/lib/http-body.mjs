// Reading a bounded HTTP request body: the one place the 8 MB cap lives (the receiver and the capture's file sink both use it).
import { gunzipSync } from 'node:zlib';

export const MAX_BODY_BYTES = 8 * 1024 * 1024;

/** The whole body as a Buffer, `{ ok: false, reason: 'too large' }` past the cap (the rest is drained, never buffered), gunzipped if sent so. */
export function readBody(req, limit = MAX_BODY_BYTES) {
  return new Promise(resolve => {
    const chunks = [];
    let size = 0;
    req.on('data', chunk => {
      size += chunk.length;
      if (size <= limit) chunks.push(chunk);
    });
    req.on('error', e => resolve({ ok: false, reason: `request error: ${e.message}` }));
    req.on('end', () => resolve(size > limit ? { ok: false, reason: 'too large' } : inflate(req, Buffer.concat(chunks), limit)));
  });
}

function inflate(req, buffer, limit) {
  if (req.headers['content-encoding'] !== 'gzip') return { ok: true, buffer };
  try {
    return { ok: true, buffer: gunzipSync(buffer, { maxOutputLength: limit }) };
  } catch (e) {
    return { ok: false, reason: `gzip body unreadable: ${e.message}` };
  }
}

/** True for `application/json` with or without parameters (`; charset=utf-8`). */
export const isJson = contentType => /^application\/json\s*(;|$)/i.test(contentType ?? '');
