using Bench.Hook;

// The hook client's whole contract in one line: whatever happens, exit zero.
//
// It runs inside the agent's own loop, before and after every tool call. A non-zero exit puts text in front
// of the operator — and, on some events, can block the call outright. Instrumentation that interferes with
// the work it measures is instrumentation somebody removes by the end of the week, and then there is no
// measurement at all.
return await HookClient.RunAsync(args, Console.In, Console.Error);
