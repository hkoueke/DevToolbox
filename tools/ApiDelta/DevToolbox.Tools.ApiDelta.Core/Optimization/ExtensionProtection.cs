using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>
/// OPT-010 : une contrainte au-dessus de toutes les autres règles. Aucune transformation ne fait
/// disparaître ni ne déplace une donnée de fournisseur.
/// </summary>
/// <remarks>
/// Deux niveaux, parce que deux risques distincts se cachent sous « ne pas casser une extension ».
/// <para>
/// Le premier est la perte. <see cref="Survived"/> compare l'avant et l'après : chaque couple nom
/// d'extension / valeur écrite qui existait doit encore exister. Une extraction remplace quatorze
/// écritures identiques par quatorze références vers une seule, si bien que le nombre d'écritures change
/// — mais la donnée, elle, est toujours là, et l'équivalence de contrat prouve séparément que le document
/// dit la même chose.
/// </para>
/// <para>
/// Le second est le déplacement, et il est plus insidieux. Une passerelle qui lit le YAML sans résoudre
/// les références ne verra plus <c>x-sg-passerelle</c> sur l'opération si l'objet qui le portait est parti
/// dans <c>components</c>. L'outil ne sait pas ce qu'une passerelle lit, et ne peut donc pas l'affirmer
/// équivalent : <see cref="Guards"/> écarte purement et simplement de toute transformation un objet
/// portant une extension sous un préfixe protégé. Les extensions hors de ces préfixes voyagent avec
/// l'objet, puisque rien ne prétend les lire autrement qu'en suivant les références.
/// </para>
/// </remarks>
public static class ExtensionProtection
{
    /// <summary>Le préfixe de toute extension de fournisseur.</summary>
    private const string Prefix = "x-";

    /// <summary>
    /// Indique si un sous-arbre peut être transformé sans porter atteinte à une donnée de fournisseur.
    /// </summary>
    /// <param name="node">Le sous-arbre visé.</param>
    /// <param name="ruleset">Ce que l'équipe a décidé, dont les préfixes protégés.</param>
    /// <param name="removing">
    /// La transformation fait disparaître le sous-arbre au lieu de le déplacer.
    /// </param>
    /// <returns><see langword="false"/> si la transformation doit renoncer à cet objet.</returns>
    /// <remarks>
    /// Le critère n'est pas le même selon ce qu'on s'apprête à faire, et la différence est de fond.
    /// <para>
    /// Un <em>déplacement</em> ne perd rien : la donnée est toujours dans le document, et toute lecture
    /// qui suit les références la retrouve. Seules les extensions sous un préfixe protégé l'interdisent,
    /// parce que là quelqu'un lit peut-être le fichier sans suivre les références — c'est tout le sens
    /// d'avoir déclaré ce préfixe.
    /// </para>
    /// <para>
    /// Une <em>suppression</em>, elle, rend la donnée introuvable, quelle que soit la façon de lire. Un
    /// composant inaccessible qui porte une extension de fournisseur n'est donc pas retiré, protégé ou
    /// non : l'outil ne sait pas ce qu'elle vaut, et c'est exactement pourquoi il ne la jette pas.
    /// </para>
    /// </remarks>
    public static bool Guards(SpecNode node, Ruleset ruleset, bool removing = false)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(ruleset);

        return !Extensions(node).Any(entry =>
            removing
            || ruleset.ProtectedExtensionPrefixes.Any(guarded =>
                entry.Name.StartsWith(guarded, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Les extensions que l'après aurait perdues par rapport à l'avant.
    /// </summary>
    /// <param name="before">L'arbre avant transformation.</param>
    /// <param name="after">L'arbre après.</param>
    /// <returns>Les noms d'extensions dont une écriture a disparu, vides quand tout a survécu.</returns>
    public static IReadOnlyList<string> Survived(SpecNode before, SpecNode after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        HashSet<string> kept = new(
            Extensions(after).Select(entry => entry.Name + "=" + entry.Written), StringComparer.Ordinal);

        return
        [
            .. Extensions(before)
                .Where(entry => !kept.Contains(entry.Name + "=" + entry.Written))
                .Select(entry => entry.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// Chaque extension de l'arbre, avec une écriture de sa valeur.
    /// </summary>
    /// <remarks>
    /// L'écriture est une forme textuelle grossière, suffisante pour constater qu'une valeur est toujours
    /// là. La forme canonique serait plus juste mais exigerait le métamodèle, et une extension n'a par
    /// définition pas de ligne dans le métamodèle : l'outil ne sait pas ce qu'elle contient, et c'est
    /// précisément pour cela qu'il n'y touche pas.
    /// </remarks>
    private static IEnumerable<(string Name, string Written)> Extensions(SpecNode node)
    {
        Stack<SpecNode> pending = new();
        pending.Push(node);

        while (pending.Count > 0)
        {
            SpecNode current = pending.Pop();

            switch (current)
            {
                case MappingNode mapping:
                    foreach (MappingEntry entry in mapping.Entries)
                    {
                        if (entry.Key.Text.StartsWith(Prefix, StringComparison.Ordinal))
                        {
                            yield return (entry.Key.Text, Written(entry.Value));
                            continue;
                        }

                        pending.Push(entry.Value);
                    }

                    break;

                case SequenceNode sequence:
                    foreach (SpecNode item in sequence.Items)
                    {
                        pending.Push(item);
                    }

                    break;
            }
        }
    }

    /// <summary>Une écriture textuelle d'un sous-arbre, clés triées pour ne pas dépendre de l'ordre.</summary>
    private static string Written(SpecNode node) => node switch
    {
        MappingNode mapping => "{" + string.Join(
            ',',
            mapping.Entries
                .Select(entry => entry.Key.Text + ":" + Written(entry.Value))
                .OrderBy(text => text, StringComparer.Ordinal)) + "}",

        SequenceNode sequence => "[" + string.Join(
            ',', sequence.Items.Select(Written)) + "]",

        ScalarNode scalar => scalar.Text,

        _ => string.Empty,
    };
}
