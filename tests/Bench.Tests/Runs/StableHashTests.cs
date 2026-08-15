using System.Security.Cryptography;
using System.Text;
using Bench.Domain;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Runs;

/// <summary>The hash four separate guarantees rest on, and which had no test of its own.
/// <para>
/// A suite version's identity, the selection/held-out split, the checkout cache's directory names and the
/// telemetry ingest's idempotency fingerprint all resolve to this function. If its output ever changes,
/// every stored fingerprint stops matching and every question silently moves to the other half of the
/// split — and both failures are invisible, because each individual run still looks internally consistent.
/// </para></summary>
public sealed class StableHashTests
{
    [Fact]
    public void The_digest_is_plain_sha256_of_the_utf8_bytes_in_lower_case_hex()
    {
        // Recomputed independently rather than pinned to a magic constant: this asserts WHAT the
        // algorithm is, so a change of algorithm fails here rather than in a report nobody reads.
        const string input = "suite:demo\nversion:1\nq1|prompt|";
        var expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));

        StableHash.Of(input).Should().Be(expected);
    }

    [Fact]
    public void The_digest_is_stable_and_is_64_lower_case_hex_characters()
    {
        var once = StableHash.Of("bench");

        StableHash.Of("bench").Should().Be(once);
        once.Should().HaveLength(64).And.MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void One_changed_character_changes_the_whole_digest()
    {
        StableHash.Of("suite:demo\nversion:1").Should().NotBe(StableHash.Of("suite:demo\nversion:2"));
    }

    [Fact]
    public void Non_ascii_input_hashes_by_its_utf8_bytes()
    {
        const string input = "вопрос про кеш · ✓";

        StableHash.Of(input).Should().Be(
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input))),
            "a suite authored with a non-ascii prompt must hash the same on every machine");
    }

    [Fact]
    public void A_bucket_is_derived_from_the_same_digest_and_never_moves()
    {
        var first = StableHash.Bucket("suite-a/q-17", 2).Ok();

        StableHash.Bucket("suite-a/q-17", 2).Ok().Should().Be(
            first, "a split that re-assigns between processes defeats itself, and does it invisibly");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(17)]
    public void A_bucket_stays_inside_its_range(int buckets)
    {
        for (var i = 0; i < 50; i++)
        {
            StableHash.Bucket($"q{i}", buckets).Ok().Should().BeInRange(0, buckets - 1);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_non_positive_bucket_count_is_refused_rather_than_thrown(int buckets)
    {
        StableHash.Bucket("q1", buckets).Reason().Should().Contain("must be positive");
    }

    [Fact]
    public void Both_halves_of_a_two_bucket_split_are_actually_used()
    {
        var buckets = Enumerable.Range(0, 200).Select(i => StableHash.Bucket($"suite/q{i}", 2).Ok()).ToList();

        buckets.Count(b => b == 0).Should().BeInRange(70, 130, "a hash that lands everything in one half is not a split");
    }
}
