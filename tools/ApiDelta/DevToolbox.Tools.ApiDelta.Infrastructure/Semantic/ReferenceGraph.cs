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
        LoadLimits limits,
        ICollection<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(diagnostics);

        Crawl crawl = new(document, policy, limits, diagnostics);

        Walk(document.Root, crawl, depth: 0);

        Report(crawl.Unresolved, document, diagnostics);

        return new ReferenceGraphResult(crawl.Edges, crawl.Cycles, crawl.Exhausted);
    }

    private static void Walk(SpecNode node, Crawl crawl, int depth)
    {
        if (crawl.Exhausted)
        {
            return;
        }

        if (depth > MaxChainDepth)
        {
            crawl.TooDeep(node);

            return;
        }

        switch (node)
        {
            case MappingNode mapping when mapping.Find(ReferenceField) is ScalarNode reference:
                Follow(reference, crawl, depth);

                break;

            case MappingNode mapping:
                foreach (MappingEntry entry in mapping.Entries)
                {
                    Walk(entry.Value, crawl, depth + 1);
                }

                break;

            case SequenceNode sequence:
                foreach (SpecNode item in sequence.Items)
                {
                    Walk(item, crawl, depth + 1);
                }

                break;
        }
    }

    private static void Follow(ScalarNode reference, Crawl crawl, int depth)
    {
        // Chaque référence rencontrée est comptée, et le budget arrête le parcours quand il est
        // atteint. Compté ici plutôt qu'après coup : un budget vérifié à la fin aurait laissé faire tout
        // le travail qu'il existe pour borner.
        if (!crawl.Count(reference))
        {
            return;
        }

        ReferenceTarget target = ReferenceTarget.Parse(reference.Text);

        if (target.IsExternal)
        {
            crawl.Edges.Add(new ReferenceEdge(
                reference.Location,
                target,
                Classify(crawl.Policy, target, reference, crawl.Diagnostics)));

            return;
        }

        SpecNode? resolved = PointerNavigator.Find(crawl.Document.Root, target.Fragment);

        if (resolved is null)
        {
            crawl.Edges.Add(
                new ReferenceEdge(reference.Location, target, ReferenceResolution.Unresolved));

            if (!crawl.Unresolved.TryGetValue(target, out List<SourcePosition>? positions))
            {
                positions = [];
                crawl.Unresolved[target] = positions;
            }

            positions.Add(reference.Position);

            return;
        }

        crawl.Edges.Add(new ReferenceEdge(
            reference.Location, target, ReferenceResolution.Resolved, resolved.Location));

        if (!crawl.Visited.Add(target))
        {
            // Déjà traversée : c'est un cycle, et entre schémas c'est légal. Consigné, non signalé.
            if (!crawl.Cycles.Contains(target))
            {
                crawl.Cycles.Add(target);
            }

            return;
        }

        Walk(resolved, crawl, depth + 1);
    }

    /// <summary>
    /// Ce qu'un parcours accumule, et le budget qu'il consomme.
    /// </summary>
    /// <remarks>
    /// Réuni en un porteur parce que le parcours passait neuf paramètres, puis onze dès qu'un budget s'y
    /// est ajouté : la liste de paramètres était devenue le véritable obstacle à l'application de la
    /// limite, et une limite qu'il est pénible d'appliquer est une limite qui ne s'applique pas.
    /// </remarks>
    private sealed class Crawl(
        SpecDocument document,
        ReferencePolicy policy,
        LoadLimits limits,
        ICollection<Diagnostic> diagnostics)
    {
        private int _seen;

        internal SpecDocument Document { get; } = document;

        internal ReferencePolicy Policy { get; } = policy;

        internal LoadLimits Limits { get; } = limits;

        internal ICollection<Diagnostic> Diagnostics { get; } = diagnostics;

        internal List<ReferenceEdge> Edges { get; } = [];

        internal Dictionary<ReferenceTarget, List<SourcePosition>> Unresolved { get; } = [];

        internal HashSet<ReferenceTarget> Visited { get; } = [];

        internal List<ReferenceTarget> Cycles { get; } = [];

        /// <summary>Le budget est épuisé : le parcours s'arrête, partout où il en est.</summary>
        internal bool Exhausted { get; private set; }

        /// <summary>
        /// Consigne l'épuisement de la profondeur, en nommant la limite et l'endroit.
        /// </summary>
        /// <param name="node">Le maillon où la traversée s'arrête.</param>
        /// <remarks>
        /// Un diagnostic plutôt qu'une exception : la traversée rend ce qu'elle a trouvé jusque-là. Mais
        /// un diagnostic ne suffisait pas — il valait avertissement, et les lecteurs du graphe
        /// n'écoutaient que les diagnostics bloquants. L'épuisement marque donc le parcours lui-même, et
        /// le graphe rendu le dit, de sorte qu'aucune conclusion ne puisse se tirer d'une traversée
        /// incomplète sans l'avoir su.
        /// </remarks>
        internal void TooDeep(SpecNode node)
        {
            if (Exhausted)
            {
                return;
            }

            Exhausted = true;

            Diagnostics.Add(new Diagnostic(
                DiagnosticCodes.LimitReached,
                DiagnosticSeverity.Warning,
                $"Les références s'enchaînent sur plus de {MaxChainDepth} maillons : la résolution "
                + "s'arrête ici, et ce qui se trouve au-delà n'a pas été vu.",
                node.Location,
                node.Position));
        }

        /// <summary>
        /// Compte une référence et dit s'il faut la suivre.
        /// </summary>
        /// <param name="node">La référence rencontrée, pour situer un refus.</param>
        /// <returns><see langword="false" /> quand le budget est épuisé.</returns>
        /// <remarks>
        /// Ce budget était déclaré, documenté, réglable par <c>limits.maxReferences</c>, et appliqué
        /// nulle part : le graphe ne recevait même pas les limites. Une équipe qui écrivait
        /// <c>maxReferences: 500</c> obtenait la preuve d'une protection qu'elle n'avait pas, et l'essai
        /// qui prétendait le vérifier plaçait son assertion sous une condition que rien ne rendait
        /// jamais vraie.
        /// <para>
        /// Un avertissement, et non une erreur, comme pour la chaîne trop longue : la traversée rend ce
        /// qu'elle a vu, et les règles qui en dépendent se déclareront non évaluées. Dit une fois, et
        /// non une fois par référence — vingt mille plaintes identiques enseignent la même chose au
        /// prix de vingt mille lignes.
        /// </para>
        /// </remarks>
        internal bool Count(SpecNode node)
        {
            if (++_seen <= Limits.MaxReferences)
            {
                return true;
            }

            if (!Exhausted)
            {
                Exhausted = true;

                Diagnostics.Add(new Diagnostic(
                    DiagnosticCodes.LimitReached,
                    DiagnosticSeverity.Warning,
                    $"Le budget de références est atteint : ce document en porte plus de "
                    + $"{Limits.MaxReferences}. La résolution s'arrête ici, ce qui se trouve au-delà "
                    + "n'a pas été vu, et la limite se règle — elle se nomme « limits.maxReferences ».",
                    node.Location,
                    node.Position));
            }

            return false;
        }
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
