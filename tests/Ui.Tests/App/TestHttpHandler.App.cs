namespace JiggerJot.Ui.Tests.Infrastructure;

/// <summary>
/// The app's half of the UI test HTTP stub (Arch A1, R159). <c>TestHttpHandler.cs</c> is the platform's and stays
/// identical across repos; a stub shape an app's own pages need — a body chosen per request, the captured request
/// bodies — is added here as further members of the same partial class. JiggerJot keeps its per-request bodies and body capture in the platform half (an adapts file), because the capture
/// runs inside SendAsync; this half is empty.
/// </summary>
public sealed partial class TestHttpHandler
{
}
