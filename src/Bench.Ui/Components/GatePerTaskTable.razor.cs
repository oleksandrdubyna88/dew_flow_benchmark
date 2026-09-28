using Bench.Contracts;
using Microsoft.AspNetCore.Components;

namespace Bench.Ui.Components;

/// <summary>The per-task table: each task × reviewer with one reading per repeat, calibration tasks marked in the row they
/// sit in, every figure in words when it is not a number.</summary>
public partial class GatePerTaskTable : ComponentBase
{
    [Parameter, EditorRequired]
    public IReadOnlyList<GatePerTaskRowDto> Rows { get; set; } = [];

    private static string Each(IReadOnlyList<GateFigureDto> figures, string format = "0.##") =>
        string.Join(" · ", figures.Select(f => GateFigureWords.Of(f, format)));
}
