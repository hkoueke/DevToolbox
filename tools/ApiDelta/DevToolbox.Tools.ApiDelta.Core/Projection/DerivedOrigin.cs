using System.Globalization;
using DevToolbox.Tools.ApiDelta.Core.Wsdl;

namespace DevToolbox.Tools.ApiDelta.Core.Projection;

/// <summary>
/// D'où un élément dérivé vient dans le contrat de service, et ce que la dérivation valait.
/// </summary>
/// <param name="Subject">L'opération ou le type dont l'élément a été dérivé.</param>
/// <param name="Location">Le document du contrat et l'endroit, dans ce document, qui l'a produit.</param>
/// <param name="Confidence">Ce que la règle de projection qui l'a produit valait.</param>
/// <remarks>
/// Un élément dérivé et un élément recopié ne doivent pas être indiscernables, et c'est tout l'objet de
/// ce type. Là où une description candidate OpenAPI donne ses propres octets, un contrat de service ne
/// donne qu'une traduction : le lecteur d'un document engendré doit pouvoir voir lequel des deux il lit,
/// sans ouvrir le rapport.
/// <para>
/// La confiance ne vaut jamais <see cref="ProjectionConfidence.NotCarried"/>. Un élément <em>écrit</em>
/// a forcément été porté, exactement ou à peu près ; ce qui n'a pas été porté n'est pas un élément mais
/// une absence, et une absence se dit par un marqueur de non-remplissage, pas par une trace d'origine.
/// Annoncer « non portée » sur un élément présent dirait au lecteur le contraire de ce qu'il a sous les
/// yeux.
/// </para>
/// </remarks>
public sealed record DerivedOrigin(
    string Subject, WsdlLocation Location, ProjectionConfidence Confidence)
{
    /// <summary>Le nom du document du contrat, sans son chemin.</summary>
    /// <remarks>
    /// Le nom, et jamais le chemin : un chemin absolu varie d'une machine à l'autre, ce qui rendrait deux
    /// exécutions identiques distinguables, et porterait le dossier personnel du développeur dans un
    /// document destiné à être relu. C'est la règle que la déclaration du document suit déjà.
    /// </remarks>
    public string Document => Path.GetFileName(Location.Document);

    /// <summary>L'endroit, dans ce document, qu'un relecteur peut suivre.</summary>
    public string At => Location.Path + string.Create(
        CultureInfo.InvariantCulture, $" (ligne {Location.Line}, colonne {Location.Column})");

    /// <summary>Ce que la projection valait, dit en un mot.</summary>
    public string Worth => Confidence == ProjectionConfidence.Approximate
        ? "approchante"
        : "portée";
}
