using DevToolbox.Tools.ApiDelta.Core.Projection;

namespace DevToolbox.Tools.ApiDelta.Core.Projection.Rules;

/// <summary>Une construction de XSD 1.1 rend le type non projetable.</summary>
/// <remarks>
/// Non projetable, et non projeté à peu près : c'est le seul endroit de la table où l'échec honnête
/// consiste à décliner. Un « assert » porte une condition que JSON Schema ne sait pas exprimer, et un
/// type projeté sans elle décrirait un contrat plus permissif que le vrai — ce qui ferait accepter un
/// message que le service refuse.
/// <para>
/// Cette règle ne s'applique à rien aujourd'hui, et ce n'est pas un oubli. Le compilateur de schémas de
/// .NET lit XSD 1.0 : un document employant « assert », « alternative » ou « openContent » est refusé
/// <em>à la lecture</em>, sous le code « WSDL-XSD11 », et aucun type n'en parvient donc à la projection.
/// La règle existe parce que la table publiée la porte, parce que le refus et elle doivent dire la même
/// chose, et parce qu'un compilateur capable de XSD 1.1 la rendrait atteignable sans rien réécrire.
/// </para>
/// <para>
/// Le prédicat l'énonce plutôt que de l'imiter. Une version antérieure se raccrochait à
/// <c>HasUncarryableParts</c>, qui parle d'attributs et de contenu ouvert : elle aurait rapporté « XSD
/// 1.1 » sur chaque type portant un attribut, c'est-à-dire sur la plupart.
/// </para>
/// </remarks>
public sealed class Xsd11Rule : GapRule
{
    /// <summary>Crée la règle.</summary>
    public Xsd11Rule()
        : base("`assert`, `alternative`, `openContent` (XSD 1.1)", ProjectionGapReason.Xsd11)
    {
    }

    /// <inheritdoc />
    public override bool Matches(ProjectionSubject subject) =>
        subject?.Type is { UsesXsd11: true };

    /// <inheritdoc />
    protected override string Construct(ProjectionSubject subject) =>
        "construction XSD 1.1 dans « " + subject.Type!.Name + " »";
}
