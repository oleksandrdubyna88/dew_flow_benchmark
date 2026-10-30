// The credential broker's route allow-list (E2.S2 D-16): the broker signs every request it forwards with the real token, so the set of
// requests it forwards is the set of things the agent can do with that token. Exactly two: the Messages call the CLI makes and its token
// counter. The decision is on the RAW request line (method and target as the client wrote them), exact and origin-form only: an
// absolute-form target, a `..` or `.` segment, an encoded slash or dot, a different case, a trailing slash or a fragment is not on the
// list, because the match never decodes or normalises anything.
export const ALLOWED_ROUTES = Object.freeze([
  Object.freeze({ method: 'POST', path: '/v1/messages' }),
  Object.freeze({ method: 'POST', path: '/v1/messages/count_tokens' }),
]);

// The query is any printable ASCII but a space and `#` (a fragment is never sent in a request target).
const QUERY = /^[\x21\x22\x24-\x7e]*$/;

/** `{ allow: true } | { allow: false, why }` for the method and the raw target of a request. */
export function routeDecision(method, target) {
  if (typeof target !== 'string' || !target.startsWith('/') || target.startsWith('//')) return { allow: false, why: 'the target is not an origin-form path' };
  const q = target.indexOf('?');
  const pathPart = q < 0 ? target : target.slice(0, q);
  const query = q < 0 ? '' : target.slice(q + 1);
  if (!QUERY.test(query)) return { allow: false, why: 'the query holds a character a request target does not carry' };
  if (!ALLOWED_ROUTES.some(r => r.method === method && r.path === pathPart)) return { allow: false, why: `${method} ${pathPart.slice(0, 120)} is not on the broker's route list` };
  return { allow: true };
}
