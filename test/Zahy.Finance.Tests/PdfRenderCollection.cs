using Xunit;

namespace Zahy.Finance;

/// <summary>
/// Serializes all QuestPDF-rendering tests (F17). Render + text/Excel extraction is CPU/GC heavy;
/// running these concurrently with each other or other render-heavy tests starved the renderer and
/// made the "BETA / NOT A VALID TAX INVOICE" assertion flake under full-suite parallel load.
/// <para>
/// Tests tagged with <c>[Collection(PdfRenderCollection.Name)]</c> run serially within this collection,
/// and <c>DisableParallelization = true</c> keeps the collection from running in parallel with any other
/// collection. Test-infra only — no behavior or assertion changes.
/// </para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PdfRenderCollection
{
    public const string Name = "PdfRender";
}
