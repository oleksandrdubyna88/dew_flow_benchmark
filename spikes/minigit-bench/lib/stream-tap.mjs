// The session's stdout as it arrives (E2.S2 step 6): every chunk to the raw file, every complete line to the stamped file as `{ t, line }`
// (`t` = epoch ms from the launcher's monotonic clock, mapClock), every parsed event to `onEvent`. A line that is not JSON is kept in both
// files and never inspected. Pure over its injected writers and clock.
import { StringDecoder } from 'node:string_decoder';

export function streamTap({ clock, writeRaw, writeStamped, onEvent }) {
  const decoder = new StringDecoder('utf8'); // a character split across two chunks is held back, not decoded as two U+FFFD
  let buffer = '';
  const events = [];
  const line = text => {
    const t = clock();
    writeStamped(`${JSON.stringify({ t, line: text })}\n`);
    let ev;
    try { ev = JSON.parse(text); } catch { return; }
    events.push(ev);
    onEvent(ev, t);
  };
  return {
    events,
    push(chunk) {
      writeRaw(chunk);
      buffer += decoder.write(chunk);
      const parts = buffer.split('\n');
      buffer = parts.pop();
      parts.filter(p => p.trim() !== '').forEach(line);
    },
    flush() {
      buffer += decoder.end();
      if (buffer.trim() !== '') line(buffer);
      buffer = '';
    },
  };
}
