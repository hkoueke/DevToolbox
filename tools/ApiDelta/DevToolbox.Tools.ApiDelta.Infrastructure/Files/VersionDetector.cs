using DevToolbox.Tools.ApiDelta.Core.Loading;
using DevToolbox.Tools.ApiDelta.Core.Syntax;

namespace DevToolbox.Tools.ApiDelta.Infrastructure.Files;

/// <summary>
/// Lit la version d'OpenAPI qu'une description déclare, ou refuse de deviner.
/// </summary>
/// <remarks>
/// Les versions prises en charge sont 3.0.x et 3.1.x. Une description qui en déclare une autre est refusée
/// en nommant celles qui le sont : lire une 3.2 comme une 3.1 reviendrait à décrire un contrat selon des
/// règles qui ne sont pas les siennes.
/// </remarks>
internal static class VersionDetector
{
    private const string VersionField = "openapi";

    internal static bool TryDetect(
        SpecNode root,
        out OpenApiVersion version,
        out Diagnostic? refusal)
    {
        version = OpenApiVersion.V30;
        refusal = null;

        if (root is not MappingNode mapping)
        {
            refusal = new Diagnostic(
                DiagnosticCodes.StructuralViolation,
                DiagnosticSeverity.Error,
                "La racine d'une description OpenAPI est une association.",
                root.Location,
                root.Position);

            return false;
        }

        if (mapping.Find(VersionField) is not ScalarNode declared)
        {
            refusal = new Diagnostic(
                DiagnosticCodes.MissingVersion,
                DiagnosticSeverity.Error,
                "Cette description ne déclare pas de champ « openapi » : rien ne dit donc selon quelles "
                    + "règles la lire.",
                root.Location,
                root.Position);

            return false;
        }

        if (declared.Text.StartsWith("3.0.", StringComparison.Ordinal))
        {
            version = OpenApiVersion.V30;
            return true;
        }

        if (declared.Text.StartsWith("3.1.", StringComparison.Ordinal))
        {
            version = OpenApiVersion.V31;
            return true;
        }

        refusal = new Diagnostic(
            DiagnosticCodes.UnsupportedVersion,
            DiagnosticSeverity.Error,
            $"La version déclarée « {declared.Text} » n'est pas prise en charge. Cet outil lit les "
                + "versions 3.0.x et 3.1.x.",
            declared.Location,
            declared.Position);

        return false;
    }
}
