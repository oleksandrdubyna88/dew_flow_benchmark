// Pure: did the account's limits, not the cell, end this session? The CLI ends such a session with a `result` event
// (is_error, api_error_status 429, a fixed sentence about a spend / session / weekly limit) and puts the same sentence into one
// synthetic assistant event carrying `error: "rate_limit"`. A cell that ends this way measured the account, not the policy: it is VOID.
export const VOID_OUTCOME = 'void: rate limit';

const LIMIT_SENTENCE = /(individual spend limit|spend limit|session limit|weekly limit|usage limit|rate.?limit)/i;

/**
 * Only the CLI's own markers count - the result envelope and the `error` field of an assistant event. A model that WRITES about rate
 * limits in its text is not one.
 */
export function rateLimitSignal(events) {
  const result = events.findLast(e => e.type === 'result');
  const viaResult = Boolean(result) && result.is_error === true && (result.api_error_status === 429 || LIMIT_SENTENCE.test(String(result.result ?? '')));
  const viaAssistant = events.some(e => e.type === 'assistant' && e.error === 'rate_limit');
  return {
    void: viaResult || viaAssistant,
    via: [...(viaResult ? ['result'] : []), ...(viaAssistant ? ['assistant-error'] : [])],
    status: result?.api_error_status ?? null,
    message: viaResult ? String(result.result ?? '').slice(0, 200) : '',
  };
}
