using Bench.Application.Bank;
using Bench.Domain;
using Bench.Domain.Bank;
using Bench.Domain.Suites;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bench.Cli;

/// <summary>
/// A run's frozen question set, rebuilt from the bank and proved by the stamp the run recorded.
///
/// <para>Extracted because a SECOND caller appeared. `bench judge` re-scores stored answers and `bench
/// resume` finishes unfinished ones, and both must reconstruct the identical selection — a copy is where the
/// two would come to disagree about which questions a run measures, which is the one disagreement neither
/// could detect from its own output.</para>
///
/// <para>The stamp is what makes it safe. A bank whose questions drifted since the run froze cannot hand a
/// later pass different reference answers under the old identity, because the stamp will not mint.</para>
/// </summary>
public static class FrozenSuite
{
    /// <summary>The run's frozen selection, rebuilt from the bank and PROVED by its stamp.
    /// <para>
    /// The same door the run froze through — the same entries, promoted and frozen by the same
    /// machinery — so the stamp either reproduces byte for byte or the rebuild is refused whole. That
    /// hash is what makes this safe: a bank whose questions drifted since the test froze cannot hand
    /// the judge different reference answers under the old stamp, because the stamp will not mint.
    /// </para></summary>
    public static async Task<Outcome<Suite>> FromBankAsync(
        AsyncServiceScope scope, Guid runId, string suiteStamp, TextWriter output, CancellationToken stopping)
    {
        var db = scope.ServiceProvider.GetRequiredService<BenchDbContext>();

        var ids = await db.Cells.AsNoTracking()
            .Where(c => c.RunId == runId)
            .Select(c => c.QuestionId)
            .Distinct()
            .ToListAsync(stopping);

        var bank = await scope.ServiceProvider.GetRequiredService<Bench.Application.Bank.IQuestionBank>()
            .QuestionsAsync(new Bench.Application.Bank.BankQuery(), stopping);

        if (bank is not Outcome<IReadOnlyList<Bench.Domain.Bank.BankEntry>>.Ok(var entries))
        {
            return Outcome<Suite>.Failure(bank.Match(_ => string.Empty, reason => reason));
        }

        var chosen = entries
            .Where(e => ids.Contains(e.Question.Question.Id))
            .OrderBy(e => e.Group.Key.Value, StringComparer.Ordinal)
            .ThenBy(e => e.Question.Ordinal)
            .ToList();

        if (chosen.Count != ids.Count)
        {
            return Outcome<Suite>.Failure(
                $"the bank holds {chosen.Count} of this run's {ids.Count} question(s) — the selection cannot be "
                + "rebuilt; judge from the suite file the test was created from, with --suite-file");
        }

        return Bench.Domain.Bank.BankFreeze.Freeze(Suite.IdOf(suiteStamp), chosen).Match(
            selection =>
            {
                if (!string.Equals(selection.Stamp, suiteStamp, StringComparison.Ordinal))
                {
                    return Outcome<Suite>.Failure(
                        $"the rebuilt selection stamps '{selection.Stamp}' where the run froze '{suiteStamp}' — "
                        + "the bank has drifted since the test was created, and a verdict against different reference "
                        + "answers would look exactly like a normal one. Judge with --suite-file");
                }

                output.WriteLine($"suite    rebuilt from the bank — stamp verified: {selection.Stamp}");
                return Outcome<Suite>.Success(selection.Suite);
            },
            Outcome<Suite>.Failure);
    }
}
