using Bench.Domain;
using Bench.Domain.Runs;

namespace Bench.Application;

/// <summary>Puts every ceiling to the runtime that must impose it, and stamps the ones it accepted.
/// <para>
/// <b>The first caller of <see cref="IModelRuntime.AcceptBudgetAsync"/>.</b> That method, its refusal
/// texts and its tests all existed before this class and nothing in a live path had ever asked a runtime
/// anything — the same shape as the crash-recovery sweep that was fully implemented and called by
/// nothing. A confirmation nobody performs is a budget nobody enforces.
/// </para>
/// <para>
/// A refusal ends the preparation rather than being logged and stepped over. The measured reason is on
/// <see cref="Budget"/>: a context ceiling was once configured, believed and reasoned from for an entire
/// series while reaching nothing at all, and a real degradation was attributed to a window that had never
/// been flooded. Continuing under an unconfirmed ceiling is how that happens again.
/// </para></summary>
public static class BudgetConfirmation
{
    public static async Task<Outcome<IReadOnlyList<Budget>>> ConfirmAsync(
        IModelRuntime runtime, IReadOnlyList<Budget> budgets, CancellationToken cancellationToken)
    {
        var confirmed = new List<Budget>(budgets.Count);

        foreach (var budget in budgets)
        {
            var accepted = await runtime.AcceptBudgetAsync(budget, cancellationToken);

            if (accepted is Outcome<string>.Fail refused)
            {
                return Outcome<IReadOnlyList<Budget>>.Failure(
                    $"the runtime cannot impose {budget.Describe} — {refused.Reason}");
            }

            confirmed.Add(budget.ConfirmedBy(((Outcome<string>.Ok)accepted).Value));
        }

        return Outcome<IReadOnlyList<Budget>>.Success(confirmed);
    }
}
