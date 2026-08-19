using Bench.Domain;

namespace Bench.Cli;

/// <summary>
/// The one projection every command needs from an <see cref="Outcome{T}"/>: the reason it failed, for a
/// line on stderr.
///
/// <para>Extracted 2026-08-19, when a fourth command was about to be written. Three copies of this
/// three-line extension already lived as private members of <c>ModelsCommand</c>, <c>QuestionsCommand</c>
/// and <c>VariantsCommand</c> — identical, and each invisible to the others. A fourth would have been the
/// point at which nobody notices the fifth, so the copies were removed rather than joined.</para>
/// </summary>
internal static class OutcomeText
{
    /// <summary>Empty on success, deliberately: every caller reaches this only inside a failure branch it
    /// has already matched, and returning something plausible instead would let a success be printed as an
    /// error nobody can explain.</summary>
    public static string Reason<T>(this Outcome<T> outcome) => outcome.Match(_ => string.Empty, reason => reason);
}
