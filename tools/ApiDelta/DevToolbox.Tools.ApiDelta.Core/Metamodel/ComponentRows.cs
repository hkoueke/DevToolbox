using DevToolbox.Tools.ApiDelta.Core.Comparison;

namespace DevToolbox.Tools.ApiDelta.Core.Metamodel;

/// <summary>
/// Les champs de <c>components</c>, des mécanismes de sécurité et des exigences de sécurité.
/// </summary>
internal static class ComponentRows
{
    /// <summary>
    /// Les familles de composants, et la sorte de ce qu'elles contiennent.
    /// </summary>
    /// <remarks>
    /// Un composant ajouté ne casse rien et un composant retiré ne casse rien <em>en soi</em> : ce qui
    /// compte est ce qui le référence. L'effet est donc inconnu à ce niveau, et l'audit des composants
    /// inaccessibles est ce qui répond réellement à la question.
    /// </remarks>
    private static readonly (string Field, SpecObjectKind Child)[] Families =
    [
        ("schemas", SpecObjectKind.Schema),
        ("responses", SpecObjectKind.Response),
        ("parameters", SpecObjectKind.Parameter),
        ("examples", SpecObjectKind.Example),
        ("requestBodies", SpecObjectKind.RequestBody),
        ("headers", SpecObjectKind.Header),
        ("securitySchemes", SpecObjectKind.SecurityScheme),
        ("links", SpecObjectKind.Link),
        ("callbacks", SpecObjectKind.Callback),
        ("pathItems", SpecObjectKind.PathItem),
    ];

    internal static void Register(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        foreach ((string field, SpecObjectKind child) in Families)
        {
            rows[(SpecObjectKind.Components, field)] = new FieldDescriptor(
                Child: child,
                Collection: CollectionKind.KeyedMap,
                WhenAdded: ChangeImpact.NonBreaking,
                WhenRemoved: ChangeImpact.Unknown,
                WhenModified: ChangeImpact.Unknown);
        }

        SecuritySchemes(rows);
    }

    private static void SecuritySchemes(Dictionary<(SpecObjectKind, string), FieldDescriptor> rows)
    {
        foreach (string field in new[]
        {
            "type", "name", "in", "scheme", "bearerFormat", "openIdConnectUrl",
        })
        {
            rows[(SpecObjectKind.SecurityScheme, field)] = FieldDescriptor.Contractual;
        }

        rows[(SpecObjectKind.SecurityScheme, "description")] = FieldDescriptor.Documentation;

        rows[(SpecObjectKind.SecurityScheme, "flows")] = new FieldDescriptor(
            Child: SpecObjectKind.OAuthFlows,
            WhenModified: ChangeImpact.Breaking);

        foreach (string flow in new[]
        {
            "implicit", "password", "clientCredentials", "authorizationCode",
        })
        {
            rows[(SpecObjectKind.OAuthFlows, flow)] = new FieldDescriptor(
                Child: SpecObjectKind.OAuthFlow,
                WhenAdded: ChangeImpact.NonBreaking,
                WhenRemoved: ChangeImpact.Breaking,
                WhenModified: ChangeImpact.Breaking);
        }

        foreach (string field in new[] { "authorizationUrl", "tokenUrl", "refreshUrl" })
        {
            rows[(SpecObjectKind.OAuthFlow, field)] = FieldDescriptor.Contractual;
        }

        // Les portées sont une association de nom à description : l'ordre n'a pas de sens, et retirer une
        // portée retire un droit que l'appelant demandait.
        rows[(SpecObjectKind.OAuthFlow, "scopes")] = new FieldDescriptor(
            Collection: CollectionKind.KeyedMap,
            WhenAdded: ChangeImpact.NonBreaking,
            WhenRemoved: ChangeImpact.Breaking,
            WhenModified: ChangeImpact.DocumentationOnly);
    }

    /// <summary>
    /// Une exigence de sécurité : une association de mécanisme à liste de portées, dont l'ordre des
    /// portées n'a pas de sens.
    /// </summary>
    internal static FieldDescriptor SecurityRequirementScopes { get; } = new(
        Collection: CollectionKind.SetLike,
        Identity: IdentityRule.ByValue,
        ValueRule: ValueAwareRule.SecurityRequirement,
        WhenAdded: ChangeImpact.Breaking,
        WhenRemoved: ChangeImpact.NonBreaking,
        WhenModified: ChangeImpact.Breaking);
}
