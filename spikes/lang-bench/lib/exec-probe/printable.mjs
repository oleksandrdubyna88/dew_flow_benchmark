// P2 and V6: text that reaches the terminal from a program or a path carries no raw control character. `\n`, `\r` and `\t` are rendered as the visible escapes (so the text stays readable, no CR can
// overwrite a line and no fake line can be forged); every other C0 and C1 control (ESC included), DEL, the Unicode line and paragraph separators and the bidi controls U+202A–202E and U+2066–2069 are removed.

const VISIBLE = Object.freeze({ '\n': '\\n', '\r': '\\r', '\t': '\\t' });
const CONTROL = /[\u0000-\u001f\u007f-\u009f\u2028\u2029\u202a-\u202e\u2066-\u2069]/g; // C0, C1, DEL, the line and paragraph separators and the bidi controls (they can reorder text on screen)

/** `text` with no raw control character: `\n`, `\r`, `\t` shown as escapes, the rest stripped. */
export const printable = text => String(text).replace(CONTROL, char => VISIBLE[char] ?? '');
