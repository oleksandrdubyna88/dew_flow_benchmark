// The cleanup stack of a launch (E2.S2 D-20): a disposer is pushed for each thing a launch creates, in creation order, and `run()` calls
// them LIFO exactly once whatever the exit path was. Each is wrapped so one failing cannot skip the next; failures are collected, never lost.
export function disposerStack() {
  const items = [];
  let finished = null;
  return {
    /** Register `fn` (sync or async) under `name`. A disposer pushed after `run()` is refused, not silently dropped. */
    push(name, fn) {
      if (finished) throw new Error(`the disposer '${name}' was pushed after the stack ran`);
      items.push({ name, fn });
    },
    get names() { return items.map(i => i.name); },
    /** Run every disposer LIFO once -> `[{ name, error }]` (the failures). A second call returns the first call's result and runs nothing. */
    run() {
      finished ??= (async () => {
        const failures = [];
        for (const { name, fn } of [...items].reverse()) {
          try { await fn(); } catch (e) { failures.push({ name, error: e?.message ?? String(e) }); }
        }
        return failures;
      })();
      return finished;
    },
  };
}
