using System.Diagnostics;
using ModelArms.Core.Ports;

namespace ModelArms.Adapters;

/// <summary>D13 on Windows: <c>Process.Start</c> of the program by the path it was handed, no shell, no window, no redirection, the environment replaced and the working directory chosen; the <see cref="Process"/> is disposed at once, so this process keeps no handle to it. <b>NOT OBSERVED — there is no Windows host here.</b> A child outlives its parent on Windows unless a job object holds it, which is declared, not
/// assumed; and <b>the runner's output is not kept in <c>pids/&lt;arm&gt;.log</c> on Windows</b> (the redirection is a POSIX file action; <c>run-all</c> says so once on this road). The runner's own lock and pid file work as everywhere (D13).
/// <para>
/// The bare-name scan (<c>ModelArmsArchitectureTests.The_module_launches_nothing_by_a_bare_name</c>) names this file as its ONE exception, because it holds <c>Process.Start</c> and <c>new ProcessStartInfo</c>; a companion scan asserts the file still starts by a variable — no string literal on the launch line. D26 (b): the path is the real one <c>ExecutableResolution</c> found, the environment is the map the verb was handed.
/// </para></summary>
internal sealed class WindowsLauncher : IDetachedLauncher
{
    public DetachedStart Start(string executable, IReadOnlyList<string> arguments, string log, IReadOnlyDictionary<string, string> environment, string workingDirectory)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = workingDirectory };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment.Clear();
        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
        }

        try
        {
            using var process = Process.Start(start);
            return process is null ? new DetachedStart.Failed(3, $"{executable} did not start") : new DetachedStart.Started();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            return new DetachedStart.Failed(3, $"{executable} could not be started: {ex.Message}");
        }
    }
}
