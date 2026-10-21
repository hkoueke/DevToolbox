using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Audit.Consistency;

/// <summary>
/// Rapporte les opérations dont la description annonce une dépréciation sans que le drapeau le dise.
/// </summary>
/// <remarks>
/// La phrase est pour un lecteur humain ; le drapeau est pour tout le reste — le portail, le client
/// engendré, la passerelle qui compte les appels à ce qui devrait disparaître. Quand seule la phrase est
/// là, l'intention est écrite dans l'endroit que personne ne lit par programme.
/// <para>
/// Le remède passe en relecture : ajouter le drapeau change ce que la description publie, et peut faire
/// apparaître un avertissement de compilation chez chaque appelant. C'est souhaitable, mais pas en
/// silence.
/// </para>
/// </remarks>
public sealed class DeprecationMetadataRule : AuditRule
{
    /// <summary>Crée la règle.</summary>
    public DeprecationMetadataRule()
        : base("AUD-205", SpecObjectKind.Operation, SpecObjectKind.Schema, SpecObjectKind.Parameter)
    {
    }

    /// <inheritdoc />
    public override RuleReport Evaluate(AuditSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        return RuleReport.Evaluated(
        [
            .. Visits
                .Distinct()
                .SelectMany(subject.Nodes)
                .Where(node => !Nodes.IsReference(node.Node))
                .Select(node => (node, Phrase: Announced(subject, node.Node)))
                .Where(pair => pair.Phrase is not null)
                .Where(pair => Nodes.Flag(pair.node.Node, "deprecated") != true)
                .Select(pair => Report(
                    $"La description annonce une dépréciation — « {pair.Phrase} » — sans que "
                        + "« deprecated » le dise.",
                    pair.node.Location,
                    "Déclarer « deprecated: true », pour que les outils le sachent aussi.")),
        ]);
    }

    /// <summary>La phrase de dépréciation présente dans la description, s'il y en a une.</summary>
    private static string? Announced(AuditSubject subject, SpecNode node)
    {
        string text = string.Concat(
            Nodes.Text(node, "description") ?? string.Empty,
            " ",
            Nodes.Text(node, "summary") ?? string.Empty);

        return subject.Ruleset.DeprecationPhrases.FirstOrDefault(phrase =>
            text.Contains(phrase, StringComparison.OrdinalIgnoreCase));
    }
}
