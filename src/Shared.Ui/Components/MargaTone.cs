namespace JiggerJot.Shared.Ui.Components;

/// <summary>
/// How Marga sits beside her line (BACKBAR-1, handoff page 02).
/// </summary>
/// <remarks>
/// <see cref="Card"/> is her at 96px with a small brass label ABOVE the sentence and the sentence in
/// the display face — the page's voice. <see cref="Inline"/> is her at 32px, sans, no label — a
/// footnote to the row above her. <see cref="None"/> keeps the pre-Tone rendering driven by
/// <c>Size</c> and <c>Compact</c>, so every call site keeps compiling and moves to a tone in its own
/// slice rather than all at once.
/// </remarks>
public enum MargaTone
{
    None,
    Card,
    Inline,
}
