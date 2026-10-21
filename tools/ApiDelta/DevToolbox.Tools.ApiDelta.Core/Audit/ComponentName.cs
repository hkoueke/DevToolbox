using System.Globalization;
using System.Text;

namespace DevToolbox.Tools.ApiDelta.Core.Audit;

/// <summary>
/// Assainit un nom de composant suggéré.
/// </summary>
/// <remarks>
/// Un nom suggéré est dérivé du document audité, et il peut finir dans un nom de fichier ou dans une
/// référence. Un nom venu de l'entrée qui traverse l'outil sans être assaini est la façon ordinaire dont
/// un outil se met à écrire là où il ne devrait pas.
/// </remarks>
public static class ComponentName
{
    private const int MaxLength = 64;

    /// <summary>Compose un nom de composant à partir d'un libellé quelconque.</summary>
    /// <param name="raw">Le libellé, tel qu'il vient du document.</param>
    /// <param name="fallback">Le nom à employer quand rien d'utilisable n'en sort.</param>
    /// <returns>Un nom en capitales initiales, fait de lettres et de chiffres seulement.</returns>
    public static string Suggest(string raw, string fallback = "Partage")
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallback);

        StringBuilder builder = new();
        bool capitalize = true;

        foreach (char character in raw)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(
                    capitalize
                        ? char.ToUpper(character, CultureInfo.InvariantCulture)
                        : character);

                capitalize = false;
                continue;
            }

            capitalize = true;
        }

        string name = builder.ToString();

        if (name.Length == 0 || char.IsDigit(name[0]))
        {
            name = fallback + name;
        }

        return name.Length <= MaxLength ? name : name[..MaxLength];
    }
}
