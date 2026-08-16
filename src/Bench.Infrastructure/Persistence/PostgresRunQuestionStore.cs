using Bench.Application.Bank;
using Bench.Domain;
using Bench.Domain.Bank;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The per-test snapshot of what was selected.
/// <para>
/// Written once, when the test is created, and never updated: it is the record of what the selection WAS,
/// and a snapshot that could be edited would answer the question it exists to answer with today's opinion.
/// </para></summary>
public sealed class PostgresRunQuestionStore(BenchDbContext db) : IRunQuestionStore
{
    public async Task<Outcome<int>> SaveAsync(
        Guid runId, IReadOnlyList<SelectedQuestion> questions, CancellationToken cancellationToken)
    {
        if (questions.Count == 0)
        {
            return Outcome<int>.Failure(
                $"run {runId} selected no questions — a test whose selection is empty measures nothing");
        }

        if (await db.RunQuestions.AnyAsync(q => q.RunId == runId, cancellationToken))
        {
            return Outcome<int>.Failure($"run {runId} already has a frozen selection — a snapshot is written once");
        }

        db.RunQuestions.AddRange(questions.Select(q => new RunQuestionRow
        {
            RunId = runId,
            QuestionId = q.QuestionId,
            GroupKey = q.Group.Value,
            Ordinal = q.Ordinal,
        }));

        await db.SaveChangesAsync(cancellationToken);

        return Outcome<int>.Success(questions.Count);
    }

    public async Task<Outcome<IReadOnlyList<SelectedQuestion>>> ForRunAsync(
        Guid runId, CancellationToken cancellationToken)
    {
        var rows = await db.RunQuestions.AsNoTracking()
            .Where(q => q.RunId == runId)
            .OrderBy(q => q.GroupKey).ThenBy(q => q.Ordinal)
            .ToListAsync(cancellationToken);

        var selected = new List<SelectedQuestion>(rows.Count);

        foreach (var row in rows)
        {
            // Refused by name rather than rendered as something else: a snapshot row that cannot be read is
            // a hand-edited row, and a report that silently drops one question of a finished test is worse
            // than one that says which row it could not read.
            if (GroupKey.Parse(row.GroupKey) is not Outcome<GroupKey>.Ok(var key))
            {
                return Outcome<IReadOnlyList<SelectedQuestion>>.Failure(
                    $"run {runId} has a snapshot row for '{row.QuestionId}' whose group '{row.GroupKey}' cannot be read");
            }

            selected.Add(new SelectedQuestion(row.QuestionId, key, row.Ordinal));
        }

        return Outcome<IReadOnlyList<SelectedQuestion>>.Success(selected);
    }
}
