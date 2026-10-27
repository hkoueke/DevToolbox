using System.Globalization;

namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Où une chose se trouve dans un contrat WSDL.</summary>
/// <param name="Document">Le document qui la porte, par son chemin.</param>
/// <param name="Path">Le chemin qu'un relecteur peut suivre dans ce document.</param>
/// <param name="Line">La ligne, à partir de un.</param>
/// <param name="Column">La colonne, à partir de un.</param>
/// <remarks>
/// Le document fait partie de l'emplacement, et ce n'est pas un détail : un contrat est un graphe de
/// fichiers, et un emplacement qui ne nommerait que la position laisserait le lecteur chercher dans vingt
/// documents lequel on désigne.
/// <para>
/// Aucune étendue d'octets, délibérément. Les étendues servent à réécrire un document en épissant ses
/// propres octets ; rien n'écrit de WSDL, si bien qu'en porter serait payer une capacité sans
/// consommateur — et hériter d'une classe de défauts dont la tranche précédente a fait deux fois les
/// frais.
/// </para>
/// </remarks>
public sealed record WsdlLocation(string Document, string Path, int Line, int Column)
{
    /// <summary>La forme affichable : le document, le chemin, la position.</summary>
    public string Display =>
        System.IO.Path.GetFileName(Document) + " → " + Path + " ("
        + string.Create(
            CultureInfo.InvariantCulture, $"ligne {Line}, colonne {Column}") + ")";

    /// <inheritdoc />
    public override string ToString() => Display;
}
