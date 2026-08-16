using Bench.Application;
using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Application;

/// <summary>The step that turns an asked-for ceiling into an enforced one.
/// <para>
/// <c>AcceptBudgetAsync</c>, its refusal texts and its own tests all existed before anything called it —
/// the same shape as the crash-recovery sweep that was implemented, tested, and invoked by nothing. These
/// tests exist to keep the caller wired: a confirmation nobody performs is a budget nobody enforces.
/// </para></summary>
public sealed class BudgetConfirmationTests
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_confirmed_budget_carries_the_runtime_that_accepted_it()
    {
        var confirmed = await BudgetConfirmation.ConfirmAsync(
            new FakeRuntime(), [Budget.Of(BudgetKind.Wall, BudgetScope.Question, 600)], Ct);

        confirmed.Should().BeOfType<Outcome<IReadOnlyList<Budget>>.Ok>();
        var budget = ((Outcome<IReadOnlyList<Budget>>.Ok)confirmed).Value.Single();
        budget.IsVerified.Should().BeTrue();
        budget.AcceptedBy.Should().Be("fake-runtime");
        budget.Describe.Should().Contain("accepted by fake-runtime");
    }

    [Fact]
    public async Task A_ceiling_the_runtime_cannot_impose_stops_the_run_rather_than_being_logged_and_stepped_over()
    {
        var refused = await BudgetConfirmation.ConfirmAsync(
            new FakeRuntime(refusing: BudgetKind.Turns),
            [Budget.Of(BudgetKind.Wall, BudgetScope.Question, 600), Budget.Of(BudgetKind.Turns, BudgetScope.Question, 25)],
            Ct);

        refused.Should().BeOfType<Outcome<IReadOnlyList<Budget>>.Fail>();
        ((Outcome<IReadOnlyList<Budget>>.Fail)refused).Reason
            .Should().Contain("cannot impose").And.Contain("Turns")
            .And.Contain("one completion has no turns",
                "the runtime's own reason travels — an operator needs to know WHICH knob does not exist");
    }

    private sealed class FakeRuntime(BudgetKind? refusing = null) : IModelRuntime
    {
        public ModelHosting Hosting => ModelHosting.Local;

        public Task<Outcome<string>> AcceptBudgetAsync(Budget budget, CancellationToken cancellationToken) =>
            Task.FromResult(budget.Kind == refusing
                ? Outcome<string>.Failure("one completion has no turns — a turn ceiling belongs to an agentic loop")
                : Outcome<string>.Success("fake-runtime"));

        public Task<Outcome<ModelAnswer>> AskAsync(ModelRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("confirmation asks nothing");
    }
}
