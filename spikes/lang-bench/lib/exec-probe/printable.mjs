// P2 and V6: text that reaches the terminal from a program or a path carries no raw control character. `\n`, `\r` and `\t` are rendered as the visible escapes (so the text stays readable, no CR can
// overwrite a line and no fake line can be forged); every other C0 and C1 control (ESC included), DEL and the Unicode line and paragraph separators are removed.

const VISIBLE = Object.freeze({ '\n': '\\n', '\r': '\\r', '\t': '\\t' });
const CONTROL = /[\u0000-\u001f\u007f-\u009f\u2028\u2029]/g;

/** `text` with no raw control character: `\n`, `\r`, `\t` shown as escapes, the rest stripped. */
export const printable = text => String(text).replace(CONTROL, char => VISIBLE[char] ?? '');
