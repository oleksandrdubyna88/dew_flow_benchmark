using Bench.Application;
using Bench.Domain;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Application;

/// <summary>Getting a solver's diff out of a solver's answer — extraction, never repair, and a refusal
/// that carries what was said instead, because the next prompt edit is made from exactly that text.</summary>
public sealed class DiffFenceTests
{
    private const string Diff =
        """
        diff --git a/src/F.cs b/src/F.cs
        --- a/src/F.cs
        +++ b/src/F.cs
        @@ -1,1 +1,1 @@
        -old
        +new
        """;

    [Fact]
    public void A_labelled_diff_fence_is_taken_whole()
    {
        DiffFence.Extract($"Here is the fix.\n\n```diff\n{Diff}\n```\nDone.").Ok()
            .Should().StartWith("diff --git a/src/F.cs");
    }

    [Fact]
    public void An_unlabelled_fence_whose_body_is_a_diff_still_counts()
    {
        DiffFence.Extract($"```\n{Diff}\n```").Ok().Should().StartWith("diff --git");
    }

    [Fact]
    public void A_code_fence_that_is_not_a_diff_is_skipped_for_one_that_is()
    {
        DiffFence.Extract($"```csharp\nvar x = 1;\n```\n\n```diff\n{Diff}\n```").Ok()
            .Should().StartWith("diff --git");
    }

    [Fact]
    public void Bare_text_that_opens_like_a_diff_is_accepted_whole()
    {
        DiffFence.Extract(Diff).Ok().Should().StartWith("diff --git");
    }

    [Fact]
    public void Prose_with_no_diff_is_a_refusal_carrying_what_was_said()
    {
        DiffFence.Extract("I would change the batching logic, probably.")
            .Reason().Should().Contain("no unified diff").And.Contain("batching logic");
    }
}
