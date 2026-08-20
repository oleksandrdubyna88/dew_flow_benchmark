using Bench.Ui;
using Bench.Ui.Pages;
using Bench.Ui.Services;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bench.Tests.Ui;

/// <summary>What a host must do to mount these pages, written as a test because forgetting it has no
/// symptom until a person looks at the screen.
///
/// <para><b>The defect this pins, observed 2026-08-20.</b> The DewFlow console registered
/// <see cref="BenchUiExtensions.AddBenchUi"/> on the SERVER and not in its WebAssembly client. Nothing
/// failed: the route resolved, the nav item highlighted, the page prerendered server-side — and then WASM
/// took over and could not construct the component. What the operator saw was a menu entry that highlighted
/// over a body that never changed, which reads as a ROUTING fault and sends the search to the router, where
/// there is nothing wrong. The sibling slice's own registration comment warns about this exact half-wiring;
/// copying the comment turned out not to be the same as heeding it.</para>
///
/// <para><b>What this can and cannot catch.</b> It pins the CONTRACT — the page is unusable without the
/// registration, so the requirement is explicit and cannot be argued away. It cannot catch a host that
/// forgets the call: that host is a WebAssembly composition root of top-level statements in another
/// repository, with no seam to assert against. That gap is real and named here rather than left implied.</para>
/// </summary>
public sealed class BenchUiRegistrationTests : BunitContext
{
    [Fact]
    public void AddBenchUi_is_what_makes_the_console_pages_constructible()
    {
        Services.AddSingleton(new ScriptedBenchApi().Client());
        Services.AddBenchUi();

        // No throw is the assertion: the page takes BenchConsoleApi through its primary constructor, so a
        // container missing it cannot build the component at all.
        Render<Benchmarking>().Markup.Should().Contain("Benchmarking");
    }

    [Fact]
    public void Without_it_the_page_cannot_be_built_at_all_rather_than_rendering_empty()
    {
        // The failure mode a host must be able to recognise. It is not a blank page and not a 404 — the
        // component never comes into existence, and the exception names the service.
        Services.AddSingleton(new ScriptedBenchApi().Client());

        var attempt = () => Render<Benchmarking>();

        attempt.Should().Throw<InvalidOperationException>()
            .WithMessage("*BenchConsoleApi*", "the message must name the missing service, because the "
                + "visible symptom points at the router instead");
    }

    [Fact]
    public void The_registration_is_idempotent_so_a_host_registering_twice_is_not_a_defect()
    {
        // Server-side prerendering and the WASM client are two containers in one product, and a host that
        // calls this from a shared helper as well as directly must not break. TryAdd is what makes that so.
        Services.AddSingleton(new ScriptedBenchApi().Client());
        Services.AddBenchUi();
        Services.AddBenchUi();

        Services.Count(service => service.ServiceType == typeof(BenchConsoleApi)).Should().Be(1);
    }
}
