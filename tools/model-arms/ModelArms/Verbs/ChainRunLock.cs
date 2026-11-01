using Bench.Cli;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;

namespace ModelArms.Verbs;

/// <summary>The run's own lock, <c>chain/&lt;run&gt;/.chain.lock</c> (E5 §8 item 45): ONE <see cref="FileGateLock.Take"/> before any stage's side effect, never waited for. The descriptor is the runtime's, opened close-on-exec, so no runner <c>Start</c> launches inherits it and a chain killed beside its live runners frees the lock with its death. The lock FILE is never deleted (§4.11: a deleted lock file lets two holders hold two files); the holder's note is removed by <see cref="FileGateLock"/> on release.
/// A holder can die between its lock and its note and a live one may not have written it yet, so <i>(holder unknown)</i> is a legal answer: the note is believed only for a live pid with its recorded start (<see cref="GateHolderNote"/>).</summary>
internal static class ChainRunLock
{
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(250);

    /// <summary>The held lock — release it by disposing it — or the stop: <c>Busy</c> and a lock path the root will not let be used are 4, the host that could not say is 3.</summary>
    public static Passage<IDisposable> Take(ExperimentInput input, string run)
    {
        var path = input.Root.ChainLock(run);
        var name = $"model-arms chain {run}";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Passage<IDisposable>.Stop(ExitCodes.Environment, $"{name}: chain/{run} cannot be made: {ex.Message}");
        }

        var created = FileGateLock.Create(TimeProvider.System, Poll, PlatformFactsReader.CurrentRoad);
        if (created is Bench.Domain.Outcome<FileGateLock>.Fail failed)
        {
            return Passage<IDisposable>.Stop(ExitCodes.Environment, $"{name}: {failed.Reason}");
        }

        var gate = ((Bench.Domain.Outcome<FileGateLock>.Ok)created).Value;
        return gate.Take(path, create: true, note: true) switch
        {
            GateLockAnswer.Held held => new Passage<IDisposable>.Through(held.Handle),
            GateLockAnswer.Busy => Passage<IDisposable>.Stop(ExitCodes.Configuration, $"{name}: another chain of {run} holds chain/{run}/.chain.lock {gate.Describe(path)} — nothing was started"),
            GateLockAnswer.Refused refused => Passage<IDisposable>.Stop(ExitCodes.Configuration, $"{name}: {refused.Reason}"),
            GateLockAnswer.Unread unread => Passage<IDisposable>.Stop(ExitCodes.Environment, $"{name}: {unread.Reason}"),
            _ => Passage<IDisposable>.Stop(ExitCodes.Environment, $"{name}: chain/{run}/.chain.lock could not be taken"),
        };
    }
}
