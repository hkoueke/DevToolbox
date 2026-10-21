namespace DevToolbox.Tools.ApiDelta.Core.Optimization;

/// <summary>Jusqu'où les membres d'un groupe se ressemblent.</summary>
/// <remarks>
/// Les deux ne se mélangent jamais dans un même groupe. Un double exact est un fait ; un quasi-doublon
/// est une ressemblance, qui peut être une négligence comme une distinction voulue. Les réunir
/// obligerait le lecteur à vérifier chaque ligne pour savoir laquelle des deux il a devant lui.
/// </remarks>
public enum DuplicateTier
{
    /// <summary>Identiques sous le profil le plus exigeant.</summary>
    Exact,

    /// <summary>Semblables, et différents par des champs nommés.</summary>
    Near,
}
