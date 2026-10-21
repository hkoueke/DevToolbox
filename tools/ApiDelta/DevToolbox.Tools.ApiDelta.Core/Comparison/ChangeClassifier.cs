using System.Globalization;
using DevToolbox.Tools.ApiDelta.Core.Metamodel;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Comparison;

/// <summary>
/// Dit ce qu'un changement fait aux appelants existants, et pourquoi.
/// </summary>
/// <remarks>
/// Deux sources, et une seule règle d'arbitrage. Le métamodèle dit ce que vaut, pour ce champ, une
/// apparition, une disparition ou une modification. Les champs dont l'effet dépend du sens du changement —
/// une obligation ajoutée, une borne abaissée, une énumération raccourcie — portent une règle sensible à la
/// valeur, et c'est elle qui l'emporte.
/// <para>
/// Ce qui ne peut pas être établi est classé inconnu, avec sa raison. Jamais « compatible » par défaut :
/// un doute annoncé se vérifie en relecture, un doute tu se découvre en production.
/// </para>
/// </remarks>
internal static class ChangeClassifier
{
    internal static Difference Classify(
        SpecPointer location,
        MetamodelEntry entry,
        DifferenceKind kind,
        SpecNode? before,
        SpecNode? after)
    {
        (ChangeImpact impact, Confidence confidence, string rationale, string changeId) = kind switch
        {
            DifferenceKind.Added => Appearance(entry, after),
            DifferenceKind.Removed => Disappearance(entry, before),
            _ => Modification(entry, before, after),
        };

        return new Difference(
            changeId,
            location,
            kind,
            before is null ? null : ValueSummary.Of(before),
            after is null ? null : ValueSummary.Of(after),
            impact,
            confidence,
            rationale,
            Advise(impact),
            entry.Policy);
    }

    private static (ChangeImpact, Confidence, string, string) Appearance(
        MetamodelEntry entry,
        SpecNode? after)
    {
        if (entry.ValueRule == ValueAwareRule.RequirednessFlag && IsTrue(after))
        {
            return (
                ChangeImpact.Breaking,
                Confidence.Certain,
                "Une obligation apparaît là où il n'y en avait pas : ce qui passait sans elle est "
                    + "désormais refusé.",
                ChangeIds.ParameterBecameRequired);
        }

        // Polarité inverse : une permission qui apparaît élargit, et l'annoncer cassant apprendrait à
        // une équipe à ne plus lire l'outil.
        if (entry.ValueRule == ValueAwareRule.PermissionFlag && IsTrue(after))
        {
            return (
                ChangeImpact.NonBreaking,
                Confidence.Certain,
                "Une permission apparaît : ce qui passait passe encore, et davantage passe.",
                ChangeIds.ParameterBecameOptional);
        }

        return (
            entry.WhenAdded,
            entry.Role == FieldRole.Vendor ? Confidence.Uncertain : Confidence.Probable,
            Explain(entry, "apparaît"),
            entry.Role == FieldRole.Documentation
                ? ChangeIds.DocumentationChanged
                : ChangeIds.FieldAdded);
    }

    private static (ChangeImpact, Confidence, string, string) Disappearance(
        MetamodelEntry entry,
        SpecNode? before)
    {
        if (entry.ValueRule == ValueAwareRule.RequirednessFlag && IsTrue(before))
        {
            return (
                ChangeImpact.NonBreaking,
                Confidence.Certain,
                "Une obligation disparaît : ce qui passait passe encore, et davantage passe.",
                ChangeIds.ParameterBecameOptional);
        }

        if (entry.ValueRule == ValueAwareRule.PermissionFlag && IsTrue(before))
        {
            return (
                ChangeImpact.Breaking,
                Confidence.Certain,
                "Une permission disparaît : ce que l'appelant envoyait est désormais refusé.",
                ChangeIds.ParameterBecameRequired);
        }

        return (
            entry.WhenRemoved,
            entry.Role == FieldRole.Vendor ? Confidence.Uncertain : Confidence.Probable,
            Explain(entry, "disparaît"),
            entry.Role == FieldRole.Documentation
                ? ChangeIds.DocumentationChanged
                : ChangeIds.FieldRemoved);
    }

    private static (ChangeImpact, Confidence, string, string) Modification(
        MetamodelEntry entry,
        SpecNode? before,
        SpecNode? after) => entry.ValueRule switch
    {
        ValueAwareRule.RequirednessFlag => Requiredness(before, after),
        ValueAwareRule.PermissionFlag => Permission(before, after),
        ValueAwareRule.UpperBound => Bound(before, after, tighteningIsLower: true),
        ValueAwareRule.LowerBound => Bound(before, after, tighteningIsLower: false),
        ValueAwareRule.TypeName => (
            ChangeImpact.Breaking,
            Confidence.Certain,
            "Le type change : ce que l'appelant envoyait ou recevait n'a plus la même nature.",
            ChangeIds.TypeChanged),
        ValueAwareRule.FormatName => (
            ChangeImpact.Breaking,
            Confidence.Probable,
            "Le format change : les clients engendrés en tirent souvent un type, et cessent de compiler.",
            ChangeIds.FormatChanged),
        ValueAwareRule.ParameterLocation => (
            ChangeImpact.Breaking,
            Confidence.Certain,
            "Le paramètre change d'emplacement : l'appelant l'envoie désormais au mauvais endroit.",
            ChangeIds.FieldChanged),
        ValueAwareRule.SecurityRequirement => (
            ChangeImpact.Breaking,
            Confidence.Probable,
            "L'exigence de sécurité change : un appelant autorisé peut cesser de l'être.",
            ChangeIds.SecurityChanged),
        _ => (
            entry.WhenModified,
            entry.Role == FieldRole.Vendor ? Confidence.Uncertain : Confidence.Probable,
            Explain(entry, "change"),
            entry.Role == FieldRole.Documentation
                ? ChangeIds.DocumentationChanged
                : ChangeIds.FieldChanged),
    };

    /// <summary>
    /// Un drapeau de permission : le retirer resserre, le poser élargit.
    /// </summary>
    /// <remarks>
    /// La polarité inverse de <see cref="Requiredness"/>. Écrite à part plutôt que paramétrée par un
    /// booléen, parce que les deux cas ont des raisons différentes à donner au lecteur et que c'est la
    /// raison qui sera lue.
    /// </remarks>
    private static (ChangeImpact, Confidence, string, string) Permission(
        SpecNode? before,
        SpecNode? after)
    {
        bool was = IsTrue(before);
        bool now = IsTrue(after);

        if (was && !now)
        {
            return (
                ChangeImpact.Breaking,
                Confidence.Certain,
                "Ce qui était permis ne l'est plus : l'appelant qui s'en servait est refusé.",
                ChangeIds.ParameterBecameRequired);
        }

        if (!was && now)
        {
            return (
                ChangeImpact.NonBreaking,
                Confidence.Certain,
                "Ce qui était refusé devient permis : rien de ce qui passait ne cesse de passer.",
                ChangeIds.ParameterBecameOptional);
        }

        return (
            ChangeImpact.NonBreaking,
            Confidence.Certain,
            "La permission est inchangée.",
            ChangeIds.DocumentationChanged);
    }

    private static (ChangeImpact, Confidence, string, string) Requiredness(
        SpecNode? before,
        SpecNode? after)
    {
        bool wasRequired = IsTrue(before);
        bool isRequired = IsTrue(after);

        if (!wasRequired && isRequired)
        {
            return (
                ChangeImpact.Breaking,
                Confidence.Certain,
                "Ce qui était facultatif devient obligatoire : l'appelant qui l'omettait est refusé.",
                ChangeIds.ParameterBecameRequired);
        }

        if (wasRequired && !isRequired)
        {
            return (
                ChangeImpact.NonBreaking,
                Confidence.Certain,
                "Ce qui était obligatoire devient facultatif : rien de ce qui passait ne cesse de passer.",
                ChangeIds.ParameterBecameOptional);
        }

        return (
            ChangeImpact.DocumentationOnly,
            Confidence.Certain,
            "Le drapeau est écrit autrement sans changer de sens.",
            ChangeIds.FieldChanged);
    }

    /// <summary>
    /// Compare deux bornes : resserrer refuse ce qui passait, élargir ne refuse rien.
    /// </summary>
    private static (ChangeImpact, Confidence, string, string) Bound(
        SpecNode? before,
        SpecNode? after,
        bool tighteningIsLower)
    {
        if (!TryNumber(before, out double previous) || !TryNumber(after, out double current))
        {
            return (
                ChangeImpact.Unknown,
                Confidence.Uncertain,
                "La borne change, et ses valeurs ne se comparent pas comme des nombres.",
                ChangeIds.FieldChanged);
        }

        bool tightened = tighteningIsLower ? current < previous : current > previous;

        return tightened
            ? (
                ChangeImpact.Breaking,
                Confidence.Certain,
                "La contrainte se resserre : ce qui était accepté peut désormais être refusé.",
                ChangeIds.ConstraintNarrowed)
            : (
                ChangeImpact.NonBreaking,
                Confidence.Certain,
                "La contrainte s'élargit : rien de ce qui passait ne cesse de passer.",
                ChangeIds.ConstraintWidened);
    }

    private static string Explain(MetamodelEntry entry, string verb) => entry.Role switch
    {
        FieldRole.Documentation => $"De la documentation {verb} : rien d'observable n'en dépend.",
        FieldRole.Vendor =>
            $"Une extension de fournisseur {verb}. L'outil n'en connaît pas la grammaire et ne peut pas "
                + "établir qu'elle est sans effet : une passerelle peut la lire.",
        _ => $"Ce champ de contrat {verb}.",
    };

    private static string Advise(ChangeImpact impact) => impact switch
    {
        ChangeImpact.Breaking =>
            "Publier une version majeure, ou revoir le changement avec les appelants concernés.",
        ChangeImpact.NonBreaking => "Publier une version mineure.",
        ChangeImpact.DocumentationOnly => "Publier un correctif.",
        _ => "Relire ce changement : l'outil ne peut pas en établir l'effet.",
    };

    private static bool IsTrue(SpecNode? node) =>
        node is ScalarNode scalar
        && string.Equals(scalar.Resolved.Canonical, "true", StringComparison.Ordinal);

    private static bool TryNumber(SpecNode? node, out double value)
    {
        value = 0;

        return node is ScalarNode scalar
            && double.TryParse(
                scalar.Resolved.Canonical, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
