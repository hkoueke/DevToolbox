namespace DevToolbox.Tools.ApiDelta.Core.Syntax;

/// <summary>La forme sous laquelle une description est écrite.</summary>
/// <remarks>
/// Détectée d'après le contenu et non d'après l'extension : un fichier nommé <c>.yaml</c> qui contient du
/// JSON est du JSON, et le dire autrement reviendrait à se tromper sur ce que l'auteur a écrit.
/// </remarks>
public enum SpecFormat
{
    /// <summary>JSON.</summary>
    Json,

    /// <summary>YAML.</summary>
    Yaml,
}
