// P2 (fix batch 2): text that reaches the terminal from a program or a path carries no control character. C0 and C1 (ESC and CR included), DEL, and the Unicode line and paragraph separators are
// removed, so a planted escape sequence or carriage return cannot rewrite a progress line, move the cursor or hide a failure.

const CONTROL = /[\u0000-\u001f\u007f-\u009f\u2028\u2029]/g;

/** `text` without any control character (the visible rest is kept, so a path stays recognisable). */
export const printable = text => String(text).replace(CONTROL, '');
