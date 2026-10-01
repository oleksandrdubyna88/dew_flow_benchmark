using System.Security.Cryptography;
using System.Text;
using Bench.Domain.Probes;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Probes;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Probes;

/// <summary>S2 — the probes' artefacts: committed under <c>probes/…</c> in the artefact root through the gate's own
/// stage → flush → rename protocol (<see cref="ArtifactCommit"/>, extracted so both stores commit one way), referenced by hash
/// and length, and never overwritten.</summary>
public sealed class ProbeArtifactsTests : IDisposable
{
    private readonly TempRoot _root = GateStoreFixtures.NewRoot();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_artefact_is_committed_once_under_the_attempts_root_and_its_ref_hashes_the_bytes()
    {
        var artifacts = new ProbeArtifacts(_root.Path);
        var scope = new ProbeAttemptScope(Guid.CreateVersion7(), Guid.CreateVersion7(), 1, 1);
        var bytes = Encoding.UTF8.GetBytes("inside.txt contains IN-7f3a9c2e1b\n");

        var answer = (await artifacts.CommitAsync(scope, ProbeArtifactKind.Answer, bytes, Ct)).Ok();

        answer.Path.Value.Should().Be($"probes/{scope.RunId:D}/{scope.CellId:D}/g1/a1/answer.txt");
        answer.Sha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(bytes)));
        answer.Length.Should().Be(bytes.Length);
        var onDisk = Path.Combine([_root.Path, .. answer.Path.Segments]);
        File.ReadAllBytes(onDisk).Should().Equal(bytes);
        Directory.EnumerateFiles(Path.GetDirectoryName(onDisk)!).Should().ContainSingle("no staging file is left behind");
        (await artifacts.CommitAsync(scope, ProbeArtifactKind.Answer, Encoding.UTF8.GetBytes("other"), Ct)).Reason().Should().Contain("already exists");
        File.ReadAllBytes(onDisk).Should().Equal(bytes, "never overwritten");
        (await artifacts.CommitAsync(scope, ProbeArtifactKind.Stderr, ReadOnlyMemory<byte>.Empty, Ct)).Ok().Length.Should().Be(0, "an empty stderr is an artefact too — 'said nothing' is a fact");
    }

    [Fact]
    public async Task The_gate_store_still_commits_through_the_same_protocol()
    {
        var store = GateStoreFixtures.Store(_root);
        var run = GateStoreFixtures.Run();
        var scope = new Bench.Domain.Gate.ArtifactScope(run, Guid.NewGuid(), 1);
        (await store.BeginAttemptAsync(scope, Ct)).Ok();
        var path = Bench.Domain.Gate.CellPaths.AttemptRoot(scope).Then("reply.json").Ok();

        var written = (await store.WriteAsync(scope, Bench.Domain.Gate.ArtifactClass.Reply, path, Encoding.UTF8.GetBytes("{}"), Ct)).Ok();

        written.Sha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("{}"))));
        (await store.WriteAsync(scope, Bench.Domain.Gate.ArtifactClass.Reply, path, Encoding.UTF8.GetBytes("{}"), Ct)).Reason()
            .Should().Be($"{path} already exists — an artefact is committed once and never overwritten", "the gate's sentence is unchanged by the extraction");
    }

    public void Dispose() => _root.Dispose();
}
