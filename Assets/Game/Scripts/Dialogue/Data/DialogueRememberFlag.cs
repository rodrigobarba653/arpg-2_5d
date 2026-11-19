using System.Text.RegularExpressions;

/// <summary>
/// Builds stable WorldState flag ids for remembered dialogue choices.
/// </summary>
public static class DialogueRememberFlag
{
    public static string ResolveId(DialogueAsset asset, DialogueChoiceOption option)
    {
        if (option == null)
            return "";

        if (!string.IsNullOrEmpty(option.rememberFlagId))
            return option.rememberFlagId.Trim();

        if (!string.IsNullOrEmpty(option.rememberAs))
            return Sanitize(option.rememberAs);

        string dialogue = asset != null ? Sanitize(asset.dialogueId) : "dialogue";
        string opt = Sanitize(option.optionId);
        if (string.IsNullOrEmpty(dialogue))
            dialogue = "dialogue";
        if (string.IsNullOrEmpty(opt))
            opt = "option";
        return dialogue + "_" + opt;
    }

    public static string Sanitize(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "";
        string s = raw.Trim().ToLowerInvariant();
        s = Regex.Replace(s, @"[^a-z0-9]+", "_");
        s = Regex.Replace(s, @"_+", "_").Trim('_');
        return s;
    }
}
