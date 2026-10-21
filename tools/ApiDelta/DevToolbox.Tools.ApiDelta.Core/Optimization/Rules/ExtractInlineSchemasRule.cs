namespace DevToolbox.Tools.ApiDelta.Core.Optimization.Rules;

/// <summary>
/// OPT-006 : extrait les schémas écrits sur place dans <c>components.schemas</c>. Désactivée par défaut.
/// </summary>
/// <remarks>
/// Désactivée, et ce n'est pas un réglage de prudence : les engendreurs de clients tirent les noms de
/// types des noms de schémas. Extraire un schéma anonyme lui donne un nom, et ce nom devient un type
/// public chez chaque appelant — un changement de surface que l'outil ne peut pas proposer en silence,
/// quelle que soit l'économie d'octets.
/// <para>
/// C'est aussi pourquoi elle reste à relire même activée : la décision n'est pas « est-ce équivalent »
/// mais « ce nom est-il celui qu'on veut publier », et personne d'autre que l'équipe ne peut y répondre.
/// </para>
/// </remarks>
public sealed class ExtractInlineSchemasRule : ExtractionRule
{
    /// <summary>Crée la règle.</summary>
    public ExtractInlineSchemasRule()
        : base("OPT-006")
    {
    }
}
