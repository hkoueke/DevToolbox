using DevToolbox.Tools.ApiDelta.Core;
using DevToolbox.Tools.ApiDelta.Core.Abstractions;
using DevToolbox.Tools.ApiDelta.Core.Configuration;
using DevToolbox.Tools.ApiDelta.Core.Dependencies;
using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Semantic;

/// <summary>
/// Parcourt les références d'un document : ce qui se résout, ce qui ne se résout pas, et ce que la
/// politique interdit.
/// </summary>
/// <remarks>
/// La traversée est la nôtre, sur l'arbre, et non celle de la bibliothèque : il faut pouvoir nommer
/// <em>chaque</em> endroit qui emploie une référence morte, et non seulement la première. Un développeur
/// qui doit corriger une référence veut la liste de ses usages, pas un exemple.
/// <para>
/// Un cycle entre schémas est légal — un dossier qui contient un dossier est une description juste — et il
/// est donc consigné, non signalé. Ce qui serait une erreur, c'est de boucler : l'ensemble des cibles déjà
/// visitées, indexé par identité canonique, est ce qui fait terminer la traversée.
/// </para>
/// </remarks>
public sealed class ReferenceGraph : IReferenceGraph
{
    private const string ReferenceField = "$ref";

    /// <summary>
    /// La profondeur au-delà de laquelle la traversée renonce et le dit.
    /// </summary>
    /// <remarks>
    /// Une chaîne de références est une forme de profondeur : un schéma qui désigne le suivant, qui
    /// désigne le suivant, sur dix mille maillons, fait descendre cette traversée de dix mille cadres de
    /// pile. Une telle description est plausible — une liste chaînée de types l'engendre — et sans cette
    /// borne l'outil se brisait par débordement de pile, c'est-à-dire de la pire façon possible : sans
    /// diagnostic, sans position, et sans rien dire de ce qu'il venait de lire.
    /// <para>
    /// La borne est celle de l'imbrication, et non le budget de références : celui-ci compte les
    /// références, celle-là mesure combien on en enchaîne. Deux limites distinctes parce que deux
    /// épuisements distincts.
    /// </para>
    /// </remarks>
    private const int MaxChainDepth = 400;

    /// <inheritdoc />
    public ReferenceGraphResult Resolve(
        SpecDocument document,
        ReferencePolicy policy,
        ICollection<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(diagnostics);

        List<ReferenceEdge> edges = [];
        Dictionary<ReferenceTarget, List<SourcePosition>> unresolved = [];
        HashSet<ReferenceTarget> visited = [];
        List<ReferenceTarget> cycles = [];

        Walk(
            document.Root, document, policy, edges, unresolved, visited, cycles, diagnostics, depth: 0);

        Report(unresolved, document, diagnostics);

        return new ReferenceGraphResult(edges, cycles);
    }

    private static void Walk(
        SpecNode node,
        SpecDocument document,
        ReferencePolicy policy,
        List<ReferenceEdge> edges,
        Dictionary<ReferenceTarget, List<SourcePosition>> unresolved,
        HashSet<ReferenceTarget> visited,
        List<ReferenceTarget> cycles,
        ICollection<Diagnostic> diagnostics,
        int depth)
    {
        if (depth > MaxChainDepth)
        {
            Exhausted(node, diagnostics);

            return;
        }

        switch (node)
        {
            case MappingNode mapping when mapping.Find(ReferenceField) is ScalarNode reference:
                Follow(
                    reference,
                    document,
                    policy,
                    edges,
                    unresolved,
                    visited,
                    cycles,
                    diagnostics,
                    depth);
                break;

            case MappingNode mapping:
                foreach (MappingEntry entry in mapping.Entries)
                {
                    Walk(
                        entry.Value,
                        document,
                        policy,
                        edges,
                        unresolved,
                        visited,
                        cycles,
                        diagnostics,
                        depth + 1);
                }

                break;

            case SequenceNode sequence:
                foreach (SpecNode item in sequence.Items)
                {
                    Walk(
                        item,
                        document,
                        policy,
                        edges,
                        unresolved,
                        visited,
                        cycles,
                        diagnostics,
                        depth + 1);
                }

                break;
        }
    }

    private static void Follow(
        ScalarNode reference,
        SpecDocument document,
        ReferencePolicy policy,
        List<ReferenceEdge> edges,
        Dictionary<ReferenceTarget, List<SourcePosition>> unresolved,
        HashSet<ReferenceTarget> visited,
        List<ReferenceTarget> cycles,
        ICollection<Diagnostic> diagnostics,
        int depth)
    {
        ReferenceTarget target = ReferenceTarget.Parse(reference.Text);

        if (target.IsExternal)
        {
            edges.Add(new ReferenceEdge(reference.Location, target, Classify(policy, target, reference, diagnostics)));
            return;
        }

        SpecNode? resolved = PointerNavigator.Find(document.Root, target.Fragment);

        if (resolved is null)
        {
            edges.Add(new ReferenceEdge(reference.Location, target, ReferenceResolution.Unresolved));

            if (!unresolved.TryGetValue(target, out List<SourcePosition>? positions))
            {
                positions = [];
                unresolved[target] = positions;
            }

            positions.Add(reference.Position);

            return;
        }

        edges.Add(new ReferenceEdge(
            reference.Location, target, ReferenceResolution.Resolved, resolved.Location));

        if (!visited.Add(target))
        {
            // Déjà traversée : c'est un cycle, et entre schémas c'est légal. Consigné, non signalé.
            if (!cycles.Contains(target))
            {
                cycles.Add(target);
            }

            return;
        }

        Walk(
            resolved,
            document,
            policy,
            edges,
            unresolved,
            visited,
            cycles,
            diagnostics,
            depth + 1);
    }

    /// <summary>
    /// Consigne l'épuisement de la profondeur, en nommant la limite et l'endroit.
    /// </summary>
    /// <remarks>
    /// Un diagnostic plutôt qu'une exception : la traversée rend ce qu'elle a trouvé jusque-là, et les
    /// règles qui dépendent du graphe se déclareront non évaluées. C'est la discipline de l'outil — une
    /// analyse incomplète le dit — appliquée à sa propre limite.
    /// </remarks>
    private static void Exhausted(SpecNode node, ICollection<Diagnostic> diagnostics)
    {
        diagnostics.Add(new Diagnostic(
            DiagnosticCodes.LimitReached,
            DiagnosticSeverity.Warning,
            $"Les références s'enchaînent sur plus de {MaxChainDepth} maillons : la résolution "
            + "s'arrête ici, et ce qui se trouve au-delà n'a pas été vu.",
            node.Location,
            node.Position));
    }

    /// <summary>
    /// Décide du sort d'une référence qui sort du document, selon la politique en vigueur.
    /// </summary>
    private static ReferenceResolution Classify(
        ReferencePolicy policy,
        ReferenceTarget target,
        ScalarNode reference,
        ICollection<Diagnostic> diagnostics)
    {
        bool remote = target.File.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || target.File.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        if (remote && !policy.AllowRemote)
        {
            diagnostics.Add(Refuse(
                reference,
                "Cette référence sort vers le réseau, ce qui est désactivé par défaut : une description "
                    + "qui dépend du réseau ne produit pas deux fois le même résultat."));

            return ReferenceResolution.DisallowedByPolicy;
        }

        if (!remote && !policy.AllowLocal)
        {
            diagnostics.Add(Refuse(
                reference,
                "Cette référence sort vers un autre fichier, ce qui est désactivé par défaut."));

            return ReferenceResolution.DisallowedByPolicy;
        }

        if (remote && !policy.RemoteHosts.Contains(HostOf(target.File), StringComparer.OrdinalIgnoreCase))
        {
            diagnostics.Add(Refuse(
                reference,
                "L'hôte de cette référence ne figure pas dans la liste des hôtes autorisés."));

            return ReferenceResolution.DisallowedByPolicy;
        }

        return ReferenceResolution.PreservedExternal;
    }

    private static string HostOf(string reference) =>
        Uri.TryCreate(reference, UriKind.Absolute, out Uri? uri) ? uri.Host : string.Empty;

    private static Diagnostic Refuse(ScalarNode reference, string message) =>
        new(
            DiagnosticCodes.DisallowedReference,
            DiagnosticSeverity.Error,
            message + " (« " + reference.Text + " »)",
            reference.Location,
            reference.Position);

    /// <summary>
    /// Signale chaque référence morte une fois, en nommant tous ses usages.
    /// </summary>
    private static void Report(
        Dictionary<ReferenceTarget, List<SourcePosition>> unresolved,
        SpecDocument document,
        ICollection<Diagnostic> diagnostics)
    {
        foreach ((ReferenceTarget target, List<SourcePosition> positions) in unresolved)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticCodes.UnresolvedReference,
                DiagnosticSeverity.Error,
                $"La référence « #{target.Fragment} » ne désigne rien. Elle est employée à "
                    + $"{positions.Count} endroit(s).",
                document.Root.Location,
                positions[0],
                positions.Skip(1).ToList()));
        }
    }
}
