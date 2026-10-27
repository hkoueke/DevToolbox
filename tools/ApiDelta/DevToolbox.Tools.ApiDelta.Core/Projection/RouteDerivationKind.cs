namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>Comment un itinéraire a été obtenu.</summary>
/// <remarks>
/// Trois mots qui ne promettent pas la même chose, et c'est pour cela qu'ils sont trois. Annoncer un
/// repli comme une dérivation laisserait croire que l'outil a reconnu quelque chose dans le nom de
/// l'opération, et un développeur passerait plus vite sur une proposition qu'il aurait dû regarder.
/// </remarks>
public enum RouteDerivationKind
{
    /// <summary>Un verbe reconnu et un sujet : la méthode et le chemin viennent tous deux du nom.</summary>
    Derived,

    /// <summary>Un sujet, mais pas de verbe reconnu : le chemin vient du nom, la méthode est « POST ».</summary>
    Partial,

    /// <summary>Rien de reconnaissable : l'itinéraire est un repli, et non une dérivation.</summary>
    Fallback,
}
