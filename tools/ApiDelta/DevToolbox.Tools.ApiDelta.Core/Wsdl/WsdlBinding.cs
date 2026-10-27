namespace DevToolbox.Tools.ApiDelta.Core.Wsdl;

/// <summary>Ce qu'une liaison impose à une opération.</summary>
/// <param name="Name">Son nom qualifié.</param>
/// <param name="Style">Le style, <c>document</c> ou <c>rpc</c>.</param>
/// <param name="Use">L'emploi du corps, <c>literal</c> ou <c>encoded</c>.</param>
/// <param name="Transport">Le transport déclaré.</param>
/// <param name="Soap">La version de SOAP.</param>
/// <param name="SoapAction">L'action déclarée pour l'opération, le cas échéant.</param>
/// <param name="Location">Où la liaison est déclarée.</param>
/// <remarks>
/// Rien de tout cela ne franchit la projection : OpenAPI n'a pas de style de liaison. C'est porté pour
/// être <em>rapporté</em> — un <c>rpc</c>/<c>encoded</c> est une liaison dont la forme des messages ne se
/// déduit pas du schéma seul, et projeter comme si elle s'en déduisait serait affirmer ce qu'on ne sait pas.
/// </remarks>
public sealed record WsdlBinding(
    string Name,
    string Style,
    string Use,
    string? Transport,
    SoapVersion Soap,
    string? SoapAction,
    WsdlLocation Location)
{
    /// <summary>Indique que la forme des messages ne se déduit pas du schéma seul.</summary>
    public bool ShapeIsNotDeterminedBySchema =>
        string.Equals(Use, "encoded", StringComparison.Ordinal)
        || string.Equals(Style, "rpc", StringComparison.Ordinal);
}
