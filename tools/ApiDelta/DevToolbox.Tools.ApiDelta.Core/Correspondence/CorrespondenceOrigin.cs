namespace DevToolbox.Tools.ApiDelta.Core.Correspondence;

/// <summary>D'où vient une entrée de correspondance.</summary>
/// <remarks>
/// L'origine est portée jusqu'au rapport parce qu'un constat ne vaut que ce que vaut la correspondance
/// au-dessus de lui. Une correspondance fausse ne produit pas un constat faux ici ou là : elle produit un
/// rapport assuré sur la mauvaise paire, et seul le fait de dire d'où chaque appariement vient permet à
/// un lecteur de s'en méfier.
/// </remarks>
public enum CorrespondenceOrigin
{
    /// <summary>Le développeur l'a écrite dans un fichier. L'outil ne la discute pas.</summary>
    Declared,

    /// <summary>L'outil l'a proposée, et le développeur l'a confirmée.</summary>
    Confirmed,

    /// <summary>L'outil l'a proposée, et elle n'a pas encore été confirmée.</summary>
    Proposed,

    /// <summary>L'outil n'a pas su choisir entre plusieurs candidats.</summary>
    Ambiguous,
}
