// The credential broker's body check (E2.S2 D-16). The broker signs every request it forwards with the real token, so a body that makes the API
// reach the network for the agent (a server tool: web search, web fetch, code execution, computer use, bash/text-editor/memory server variants; an
// MCP connector; a container; an image or document fetched from a URL) would let the agent use the account's reach, not only its model. The CLI's
// own tools are CLIENT tools: no `type`, or `type: "custom"`. The check is a closed rule on what is present, not a list of known bad names: a tool
// entry whose `type` is present and is not "custom" is refused, so a server tool added next year is refused too.
//
// The body is read whole (bounded) and the SAME bytes are forwarded, so what was checked is what the API reads. Two parsers can read one body
// differently, so a body they could disagree on is refused: not UTF-8, not one JSON object, a duplicate key at any depth (the last wins in
// `JSON.parse`; another parser may take the first), a content-encoding (the bytes cannot be read). A refusal names the offending key or type,
// never a value of the body.
export const MAX_BODY_BYTES = 8 * 1024 * 1024;

/** Top-level keys that make the API reach out for the agent. */
const REACHING_KEYS = Object.freeze(['mcp_servers', 'container']);
const MAX_WALK_NODES = 2_000_000;
const clip = text => String(text).slice(0, 80);

const refuse = (why, offending = null) => ({ allow: false, why, ...(offending === null ? {} : { offending: clip(offending) }) });

/** The first key that appears twice in one object of a VALID JSON text, or null. Keys are compared decoded (an escape spells the same key). */
function duplicateKey(text) {
  const stack = []; // a Set of the keys seen for an object, null for an array
  let expectKey = false;
  let i = 0;
  while (i < text.length) {
    const c = text[i];
    if (c === '"') {
      let j = i + 1;
      while (text[j] !== '"') j += text[j] === '\\' ? 2 : 1;
      if (expectKey && stack.at(-1) instanceof Set) {
        const key = JSON.parse(text.slice(i, j + 1));
        if (stack.at(-1).has(key)) return key;
        stack.at(-1).add(key);
        expectKey = false;
      }
      i = j + 1;
    } else {
      if (c === '{') { stack.push(new Set()); expectKey = true; }
      else if (c === '[') { stack.push(null); expectKey = false; }
      else if (c === '}' || c === ']') { stack.pop(); expectKey = false; }
      else if (c === ',') expectKey = stack.at(-1) instanceof Set;
      else if (c === ':') expectKey = false;
      i += 1;
    }
  }
  return null;
}

/** The first `source` of `type: "url"` anywhere in `root` (an image or document the API would fetch), as a path, or null. Iterative and bounded. */
function urlSourceIn(root, rootName) {
  const todo = [[root, rootName]];
  for (let seen = 0; todo.length > 0 && seen < MAX_WALK_NODES; seen += 1) {
    const [node, at] = todo.pop();
    if (node === null || typeof node !== 'object') continue;
    if (!Array.isArray(node) && node.source !== null && typeof node.source === 'object' && node.source.type === 'url') return `${at}.source.type=url`;
    for (const [k, v] of Object.entries(node)) todo.push([v, Array.isArray(node) ? `${at}[${k}]` : `${at}.${k}`]);
  }
  return null;
}

/** The first tool entry whose `type` is present and not "custom": its path, or null; `tools` that is no list of objects is its own refusal. */
function serverToolIn(tools) {
  if (!Array.isArray(tools)) return refuse('`tools` is not a list: the body is not read as the API reads a tool list', 'tools');
  for (const [i, tool] of tools.entries()) {
    if (tool === null || typeof tool !== 'object' || Array.isArray(tool)) return refuse(`tools[${i}] is not an object`, `tools[${i}]`);
    if ('type' in tool && tool.type !== 'custom') return refuse('a tool with a `type` other than "custom" is a server tool: the API would act for the agent', `tools[${i}].type=${tool.type}`);
  }
  return null;
}

/** `{ allow: true } | { allow: false, why, offending? }` for a request body read whole. */
export function bodyDecision(bytes) {
  let text;
  try { text = new TextDecoder('utf-8', { fatal: true }).decode(bytes); } catch { return refuse('the body is not valid UTF-8'); }
  let body;
  try { body = JSON.parse(text); } catch { return refuse('the body is not JSON'); }
  if (body === null || typeof body !== 'object' || Array.isArray(body)) return refuse('the body is not one JSON object');
  const dup = duplicateKey(text);
  if (dup !== null) return refuse('the body repeats a key: two parsers could read it differently', dup);
  const key = REACHING_KEYS.find(k => k in body);
  if (key !== undefined) return refuse(`\`${key}\` makes the API reach the network for the agent`, key);
  if ('tools' in body) {
    const bad = serverToolIn(body.tools);
    if (bad !== null) return bad;
  }
  for (const part of ['messages', 'system']) {
    const at = part in body ? urlSourceIn(body[part], part) : null;
    if (at !== null) return refuse('a source fetched from a URL makes the API reach the network for the agent', at);
  }
  return { allow: true };
}

/** `{ allow: true } | { allow: false, why }`: a body with a content-encoding cannot be read, so it is not forwarded. */
export function encodingDecision(headers) {
  const enc = String(headers['content-encoding'] ?? 'identity').trim().toLowerCase();
  return enc === 'identity' ? { allow: true } : refuse(`the body has a content-encoding (${clip(enc)}): it cannot be read, so it is not forwarded`);
}
