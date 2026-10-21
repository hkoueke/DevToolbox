namespace DevToolbox.Tools.ApiDelta.Core.Versioning;

/// <summary>Une version imposée par le développeur, et ce qu'elle vaut face à la recommandation.</summary>
/// <param name="Requested">La version demandée.</param>
/// <param name="BelowRecommendation">Elle est inférieure à ce que les changements commandent.</param>
/// <param name="Forced">Le développeur a confirmé explicitement vouloir passer outre.</param>
/// <remarks>
/// Une version inférieure à la recommandation n'est pas interdite — l'outil ne gouverne pas les équipes —
/// mais elle demande un geste explicite et figure en évidence dans le rapport. Publier en silence une
/// version mineure sur un changement cassant est la décision qu'il faut rendre impossible par accident.
/// </remarks>
public sealed record VersionOverride(SemanticVersion Requested, bool BelowRecommendation, bool Forced);
