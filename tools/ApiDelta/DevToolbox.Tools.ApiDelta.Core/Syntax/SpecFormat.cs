namespace DevToolbox.Tools.ApiDelta.Core.Syntax;

/// <summary>La forme sous laquelle une description est écrite.</summary>
/// <remarks>
/// Détectée d'après le contenu et non d'après l'extension : un fichier nommé <c>.yaml</c> qui contient du
/// JSON est du JSON, et le dire autrement reviendrait à se tromper sur ce que l'auteur a écrit.
/// </remarks>
public enum SpecFormat
{
    /// <summary>JSON.</summary>
    Json,

    /// <summary>YAML.</summary>
    Yaml,

    /// <summary>
    /// Une description qui n'a jamais été écrite : projetée depuis un contrat d'un autre langage.
    /// </summary>
    /// <remarks>
    /// Ses nœuds ne portent aucune étendue d'octets, parce qu'ils n'ont occupé aucun octet. L'émetteur
    /// fidèle et l'épisseur lisent des étendues ; les pointer sur un document projeté produirait des
    /// octets dénués de sens. La forme est nommée ici plutôt que déduite d'une convention, afin que
    /// l'absence soit visible dans le type plutôt que découverte à l'usage.
    /// <para>
    /// Rien ne s'écrit sous cette forme : elle désigne une entrée de comparaison, jamais une sortie.
    /// </para>
    /// </remarks>
    Projected,
}
