using System.Globalization;
using System.Text;

namespace Bruinet.Scraper.Explore;

/// <summary>
/// Words that say an element changes something, by category, in English, Dutch, German and French.
/// Matched as whole words (a phrase as consecutive words), ignoring case and accents, in text split at
/// spaces, punctuation, dashes, underscores and camelCase humps (<c>deleteButton</c> is "delete button").
/// </summary>
public static class DangerWords
{
    /// <summary>The words and phrases of each category.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ByCategory = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["sign-out"] = ["log out", "logout", "log off", "logoff", "sign out", "signout", "sign off", "afmelden", "uitloggen", "abmelden", "ausloggen", "se deconnecter", "deconnexion"],
        ["delete"] = ["delete", "remove", "destroy", "discard", "trash", "verwijderen", "verwijder", "wissen", "loschen", "entfernen", "supprimer", "effacer"],
        ["update"] = ["save", "submit", "update", "apply", "opslaan", "bijwerken", "toepassen", "speichern", "aktualisieren", "ubernehmen", "enregistrer", "valider", "mettre a jour", "edit", "bewerken", "wijzigen", "bearbeiten", "andern", "modifier"],
        ["send"] = ["send", "invite", "publish", "share", "verzenden", "versturen", "uitnodigen", "publiceren", "delen", "senden", "einladen", "veroffentlichen", "teilen", "envoyer", "inviter", "publier", "partager"],
        ["pay"] = ["pay", "purchase", "buy", "checkout", "check out", "place order", "betalen", "kopen", "bestellen", "bezahlen", "kaufen", "payer", "acheter", "commander"],
        ["approve"] = ["confirm", "approve", "reject", "decline", "archive", "bevestigen", "goedkeuren", "afwijzen", "archiveren", "bestatigen", "genehmigen", "ablehnen", "archivieren", "confirmer", "approuver", "rejeter", "archiver"],
        ["bulk"] = ["import", "upload", "sync", "synchronize", "reset", "clear all", "deactivate", "close account", "importeren", "uploaden", "synchroniseren", "resetten", "deactiveren", "hochladen", "synchronisieren", "zurucksetzen", "deaktivieren", "importer", "televerser", "synchroniser", "reinitialiser", "desactiver"],
    };

    /// <summary>The first danger word in <paramref name="texts"/> of a category that is on, with its category; null when none.</summary>
    public static (string Category, string Word)? Find(IEnumerable<string?> texts, Func<string, bool> isOn)
    {
        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;
            var words = Words(text);
            if (words.Count == 0)
                continue;
            foreach (var (category, phrases) in ByCategory)
            {
                if (!isOn(category))
                    continue;
                foreach (var phrase in phrases)
                    if (HasPhrase(words, Words(phrase)))
                        return (category, phrase);
            }
        }
        return null;
    }

    /// <summary>True when <paramref name="text"/> holds <paramref name="phrase"/> as whole, consecutive words, ignoring case and accents.</summary>
    public static bool Contains(string? text, string phrase) =>
        !string.IsNullOrWhiteSpace(text) && HasPhrase(Words(text), Words(phrase));

    private static bool HasPhrase(IReadOnlyList<string> words, IReadOnlyList<string> parts)
    {
        if (parts.Count == 0)
            return false;
        for (var i = 0; i + parts.Count <= words.Count; i++)
        {
            var j = 0;
            while (j < parts.Count && words[i + j] == parts[j])
                j++;
            if (j == parts.Count)
                return true;
        }
        return false;
    }

    /// <summary>A text's words: lower case, without accents, split at anything not a letter or digit and at camelCase humps.</summary>
    public static IReadOnlyList<string> Words(string text)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        char previous = ' ';
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (!char.IsLetterOrDigit(c))
            {
                Flush();
            }
            else
            {
                if (char.IsUpper(c) && char.IsLower(previous))
                    Flush();
                word.Append(char.ToLowerInvariant(c));
            }
            previous = c;
        }
        Flush();
        return words;

        void Flush()
        {
            if (word.Length > 0)
                words.Add(word.ToString());
            word.Clear();
        }
    }
}
