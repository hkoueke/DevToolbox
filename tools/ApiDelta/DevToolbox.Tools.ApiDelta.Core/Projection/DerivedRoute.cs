namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>Un itinéraire dérivé du nom d'une opération.</summary>
/// <param name="Method">La méthode HTTP.</param>
/// <param name="Path">Le chemin.</param>
/// <param name="Kind">Comment il a été obtenu.</param>
/// <param name="Verb">Le verbe reconnu, le cas échéant.</param>
/// <remarks>
/// Le verbe reconnu est porté pour que le rapport puisse dire sur quoi la dérivation s'est fondée. Un
/// développeur qui voit « POST /documents, d'après le verbe « deposer » » peut contredire la proposition
/// en connaissance de cause ; devant « POST /documents » seul, il ne peut que l'accepter ou la refuser.
/// </remarks>
public sealed record DerivedRoute(
    string Method, string Path, RouteDerivationKind Kind, string? Verb)
{
    /// <summary>L'itinéraire tel qu'il s'écrit dans un rapport.</summary>
    public string Display => Method + " " + Path;
}
