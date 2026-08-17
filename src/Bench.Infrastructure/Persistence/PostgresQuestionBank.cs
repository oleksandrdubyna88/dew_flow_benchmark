using Bench.Application;
using Bench.Application.Bank;
using Bench.Domain;
using Bench.Domain.Authoring;
using Bench.Domain.Bank;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The durable question bank.
/// <para>
/// Every uniqueness rule here is enforced by an INDEX and merely explained by a check: a group key, a
/// reviewer key, a question's suite-facing id, one mark per reviewer per question. The checks produce a
/// readable refusal; the indexes are what hold when two imports of the same file race, which is the normal
/// shape of an operator re-running a command they were not sure took.
/// </para></summary>
public sealed class PostgresQuestionBank(BenchDbContext db) : IQuestionBank
{
    public async Task<Outcome<QuestionGroup>> AddGroupAsync(QuestionGroup group, CancellationToken cancellationToken)
    {
        if (await db.QuestionGroups.AnyAsync(g => g.Key == group.Key.Value, cancellationToken))
        {
            return Outcome<QuestionGroup>.Failure($"the group '{group.Key}' is already in the bank");
        }

        db.QuestionGroups.Add(new QuestionGroupRow
        {
            Id = group.Id,
            Key = group.Key.Value,
            Title = group.Title,
            Ordinal = group.Ordinal,
        });

        return await SaveAsync(group, $"the group '{group.Key}' is already in the bank", cancellationToken);
    }

    public async Task<Outcome<Reviewer>> AddReviewerAsync(Reviewer reviewer, CancellationToken cancellationToken)
    {
        if (await db.Reviewers.AnyAsync(r => r.Key == reviewer.Key.Value, cancellationToken))
        {
            return Outcome<Reviewer>.Failure($"the reviewer '{reviewer.Key}' is already in the bank");
        }

        db.Reviewers.Add(new ReviewerRow
        {
            Id = reviewer.Id,
            Key = reviewer.Key.Value,
            DisplayName = reviewer.DisplayName,
            Ordinal = reviewer.Ordinal,
        });

        return await SaveAsync(reviewer, $"the reviewer '{reviewer.Key}' is already in the bank", cancellationToken);
    }

    public async Task<Outcome<BankQuestion>> AddAsync(BankQuestion question, CancellationToken cancellationToken)
    {
        if (await db.BankQuestions.AnyAsync(q => q.QuestionId == question.Question.Id, cancellationToken))
        {
            return Taken(question.Question.Id);
        }

        db.BankQuestions.Add(ToRow(question));

        return await SaveAsync(question, Taken(question.Question.Id).Reason(), cancellationToken);
    }

    public async Task<Outcome<IReadOnlyList<QuestionGroup>>> GroupsAsync(CancellationToken cancellationToken)
    {
        var rows = await db.QuestionGroups.AsNoTracking()
            .OrderBy(g => g.Ordinal).ThenBy(g => g.Key)
            .ToListAsync(cancellationToken);

        return All(rows, row => QuestionGroup.Rehydrate(row.Id, row.Key, row.Title, row.Ordinal));
    }

    public async Task<Outcome<IReadOnlyList<Reviewer>>> ReviewersAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Reviewers.AsNoTracking()
            .OrderBy(r => r.Ordinal).ThenBy(r => r.Key)
            .ToListAsync(cancellationToken);

        return All(rows, row => Reviewer.Rehydrate(row.Id, row.Key, row.DisplayName, row.Ordinal));
    }

    public async Task<Outcome<IReadOnlyList<BankEntry>>> QuestionsAsync(
        BankQuery query, CancellationToken cancellationToken)
    {
        var rows = await Filtered(query)
            .OrderBy(q => q.Group!.Ordinal).ThenBy(q => q.Ordinal).ThenBy(q => q.QuestionId)
            .ToListAsync(cancellationToken);

        return All(rows, ToDomain);
    }

    public async Task<Outcome<QuestionReview>> ReviewAsync(
        string questionId,
        string reviewerKey,
        ReviewVerdict verdict,
        string note,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var question = await db.BankQuestions.AsNoTracking()
            .FirstOrDefaultAsync(q => q.QuestionId == questionId, cancellationToken);
        var reviewer = await db.Reviewers.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Key == reviewerKey, cancellationToken);

        if (question is null || reviewer is null)
        {
            return Outcome<QuestionReview>.Failure(question is null
                ? $"no question '{questionId}' in the bank"
                : $"no reviewer '{reviewerKey}' in the bank — a reviewer is a row, so add it before marking with it");
        }

        return await MarkAsync(question.Id, reviewer.Id, verdict, note, at, cancellationToken);
    }

    public async Task<Outcome<IReadOnlyList<QuestionReview>>> ReviewsAsync(
        IReadOnlyList<Guid> questionIds, CancellationToken cancellationToken)
    {
        var rows = await db.QuestionReviews.AsNoTracking()
            .Where(r => questionIds.Contains(r.QuestionId))
            .OrderBy(r => r.At)
            .ToListAsync(cancellationToken);

        return Outcome<IReadOnlyList<QuestionReview>>.Success(
            [.. rows.Select(r => new QuestionReview(r.QuestionId, r.ReviewerId, r.Verdict, r.Note, r.At))]);
    }

    public async Task<Outcome<BankQuestion>> SetStateAsync(
        string questionId, CandidateState state, CancellationToken cancellationToken)
    {
        var changed = await db.BankQuestions
            .Where(q => q.QuestionId == questionId)
            .ExecuteUpdateAsync(s => s.SetProperty(q => q.State, state), cancellationToken);

        return changed == 0
            ? Outcome<BankQuestion>.Failure($"no question '{questionId}' in the bank")
            : await FindAsync(questionId, cancellationToken);
    }

    public async Task<Outcome<GroupMove>> MoveAsync(
        string questionId,
        string toGroupKey,
        string reason,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var question = await db.BankQuestions.Include(q => q.Group!)
            .FirstOrDefaultAsync(q => q.QuestionId == questionId, cancellationToken);
        var target = await db.QuestionGroups.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Key == toGroupKey, cancellationToken);

        if (question is null || target is null)
        {
            return Outcome<GroupMove>.Failure(question is null
                ? $"no question '{questionId}' in the bank"
                : $"no group '{toGroupKey}' in the bank");
        }

        return question.GroupId == target.Id
            ? Outcome<GroupMove>.Failure($"question '{questionId}' is already in '{toGroupKey}'")
            : await RecordMoveAsync(question, target, reason, at, cancellationToken);
    }

    public async Task<Outcome<IReadOnlyList<GroupMove>>> MovesAsync(
        string questionId, CancellationToken cancellationToken)
    {
        var rows = await db.QuestionGroupMoves.AsNoTracking()
            .Where(m => db.BankQuestions.Any(q => q.Id == m.QuestionId && q.QuestionId == questionId))
            .OrderBy(m => m.At)
            .ToListAsync(cancellationToken);

        return Outcome<IReadOnlyList<GroupMove>>.Success(
            [.. rows.Select(Move).Where(m => m is Outcome<GroupMove>.Ok).Select(m => ((Outcome<GroupMove>.Ok)m).Value)]);
    }

    private async Task<Outcome<GroupMove>> RecordMoveAsync(
        BankQuestionRow question,
        QuestionGroupRow target,
        string reason,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var from = question.Group?.Key ?? string.Empty;

        db.QuestionGroupMoves.Add(new QuestionGroupMoveRow
        {
            Id = Guid.CreateVersion7(),
            QuestionId = question.Id,
            FromGroup = from,
            ToGroup = target.Key,
            Reason = reason,
            At = at,
        });

        question.GroupId = target.Id;
        question.Group = null;
        await db.SaveChangesAsync(cancellationToken);

        return Move(new QuestionGroupMoveRow
        {
            QuestionId = question.Id, FromGroup = from, ToGroup = target.Key, Reason = reason, At = at,
        });
    }

    private async Task<Outcome<QuestionReview>> MarkAsync(
        Guid questionId,
        Guid reviewerId,
        ReviewVerdict verdict,
        string note,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var existing = await db.QuestionReviews
            .FirstOrDefaultAsync(r => r.QuestionId == questionId && r.ReviewerId == reviewerId, cancellationToken);

        // Replacing THIS reviewer's mark, never any other's: a reviewer changing their mind is ordinary,
        // and "two of three approved" has to stay representable while it happens.
        if (existing is null)
        {
            db.QuestionReviews.Add(new QuestionReviewRow
            {
                Id = Guid.CreateVersion7(),
                QuestionId = questionId,
                ReviewerId = reviewerId,
                Verdict = verdict,
                Note = note,
                At = at,
            });
        }
        else
        {
            existing.Verdict = verdict;
            existing.Note = note;
            existing.At = at;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Outcome<QuestionReview>.Success(new QuestionReview(questionId, reviewerId, verdict, note, at));
    }

    private async Task<Outcome<BankQuestion>> FindAsync(string questionId, CancellationToken cancellationToken)
    {
        var row = await db.BankQuestions.AsNoTracking().Include(q => q.Group!)
            .FirstOrDefaultAsync(q => q.QuestionId == questionId, cancellationToken);

        return row is null
            ? Outcome<BankQuestion>.Failure($"no question '{questionId}' in the bank")
            : ToDomain(row).Match(entry => Outcome<BankQuestion>.Success(entry.Question), Outcome<BankQuestion>.Failure);
    }

    private IQueryable<BankQuestionRow> Filtered(BankQuery query)
    {
        var rows = db.BankQuestions.AsNoTracking().Include(q => q.Group!).AsQueryable();

        // Empty and zero mean "unbounded" — the shape a CLI's unset flags arrive in.
        rows = query.GroupKey.Length > 0 ? rows.Where(q => q.Group!.Key == query.GroupKey) : rows;
        rows = query.FromOrdinal > 0 ? rows.Where(q => q.Ordinal >= query.FromOrdinal) : rows;
        rows = query.ToOrdinal > 0 ? rows.Where(q => q.Ordinal <= query.ToOrdinal) : rows;

        return query.OnlyAccepted ? rows.Where(q => q.State == CandidateState.Accepted) : rows;
    }

    private async Task<Outcome<T>> SaveAsync<T>(T value, string conflict, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // The index, not the check above, is what actually held: two imports of one file racing is the
            // normal shape of an operator re-running a command they were not sure took.
            //
            // But it is not the ONLY thing that can refuse a write, and until 2026-08-17 this said the id was
            // taken whatever went wrong. It cost an hour: an authored question whose seed date carried a local
            // offset was refused by Postgres for that, and reported as a duplicate id — a refusal that sent the
            // reader looking for a row that did not exist. The database's own sentence is appended so the two
            // cases are never again one message.
            return Outcome<T>.Failure($"{conflict}. The store said: {Cause(ex)}");
        }

        return Outcome<T>.Success(value);
    }

    /// <summary>The innermost reason, which is the one that names the constraint or the column. The outer
    /// sentence is always the same generic "an error occurred while saving the entity changes".</summary>
    private static string Cause(Exception failure) =>
        failure.InnerException is { } inner ? Cause(inner) : Line(failure.Message);

    private static string Line(string message) => message.Split('\n')[0].Trim();

    /// <summary>Reads every row or fails by name. A row that cannot be read is never skipped: a listing
    /// quietly missing a question is a selection quietly missing a question.</summary>
    private static Outcome<IReadOnlyList<TValue>> All<TRow, TValue>(
        IReadOnlyList<TRow> rows, Func<TRow, Outcome<TValue>> read)
    {
        var values = new List<TValue>(rows.Count);

        foreach (var row in rows)
        {
            if (read(row) is not Outcome<TValue>.Ok(var value))
            {
                return Outcome<IReadOnlyList<TValue>>.Failure(read(row).Reason());
            }

            values.Add(value);
        }

        return Outcome<IReadOnlyList<TValue>>.Success(values);
    }

    private static Outcome<GroupMove> Move(QuestionGroupMoveRow row) =>
        GroupKey.Parse(row.ToGroup).Match(
            to => GroupKey.Parse(row.FromGroup).Match(
                from => Outcome<GroupMove>.Success(new GroupMove(row.QuestionId, from, to, row.Reason, row.At)),
                Outcome<GroupMove>.Failure),
            Outcome<GroupMove>.Failure);

    private static Outcome<BankQuestion> Taken(string questionId) =>
        Outcome<BankQuestion>.Failure(
            $"the question id '{questionId}' is already in the bank — it is the identity every cell and every "
            + "result carries, so two questions cannot share it");

    private static BankQuestionRow ToRow(BankQuestion question) => new()
    {
        Id = question.Id,
        GroupId = question.GroupId,
        Ordinal = question.Ordinal,
        QuestionId = question.Question.Id,
        Kind = question.Kind,
        CodeTaskJson = question.CodeTaskJson,
        Prompt = question.Question.Prompt,
        ReferenceAnswer = question.Question.ReferenceAnswer,
        ExpectationsJson = QuestionJson.WriteExpectations(question.Question.Expectations),
        TargetRepoUrl = question.TargetRepo.Value,
        AuthoredAtCommit = question.AuthoredAt.Value,
        SourceKind = question.Source,
        AuthorModel = question.AuthorModel,
        SeedKind = question.Seed.Kind,
        SeedReference = question.Seed.Reference,
        SeedAt = question.Seed.At,
        State = question.State,
        CreatedAt = question.CreatedAt,
    };

    private static Outcome<BankEntry> ToDomain(BankQuestionRow row) =>
        CommitSha.Parse(row.AuthoredAtCommit).Match(
            commit => QuestionJson.ReadExpectations(row.ExpectationsJson, commit).Match(
                expectations => RepoUrl.Parse(row.TargetRepoUrl).Match(
                    repo => Entry(row, commit, repo, expectations),
                    Outcome<BankEntry>.Failure),
                Outcome<BankEntry>.Failure),
            reason => Outcome<BankEntry>.Failure($"question '{row.QuestionId}' cannot be read — {reason}"));

    private static Outcome<BankEntry> Entry(
        BankQuestionRow row, CommitSha commit, RepoUrl repo, IReadOnlyList<Expectation> expectations) =>
        QuestionGroup.Rehydrate(row.Group?.Id ?? row.GroupId, row.Group?.Key, row.Group?.Title, row.Group?.Ordinal ?? 0).Match(
            group => Outcome<BankEntry>.Success(new BankEntry(
                new BankQuestion(
                    row.Id,
                    row.GroupId,
                    row.Ordinal,
                    row.Kind,
                    new Question(row.QuestionId, row.Prompt, expectations, row.ReferenceAnswer),
                    row.CodeTaskJson,
                    row.SourceKind,
                    row.AuthorModel,
                    new QuestionSeed(row.SeedKind, row.SeedReference, row.SeedAt),
                    row.State,
                    repo,
                    commit,
                    row.CreatedAt),
                group)),
            reason => Outcome<BankEntry>.Failure($"question '{row.QuestionId}' cannot be read — {reason}"));
}

file static class OutcomeText
{
    public static string Reason<T>(this Outcome<T> outcome) => outcome.Match(_ => string.Empty, reason => reason);
}
