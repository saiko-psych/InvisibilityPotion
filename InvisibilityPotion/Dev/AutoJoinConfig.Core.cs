#if DEBUG
using System;

namespace InvisibilityPotion.Dev
{
    /// <summary>
    /// Pure parsing of the dev auto-join configuration. No game or BepInEx types, so it can be unit tested.
    /// </summary>
    internal static class AutoJoinConfig
    {
        /// <summary>
        /// Reads "world" and "character" from key=value file content (may be null). Blank lines and lines
        /// starting with '#' are ignored, keys are case-insensitive, whitespace is trimmed. Environment values
        /// are a fallback used only when the file does not provide a non-empty value for that key.
        /// </summary>
        internal static (string world, string character) Parse(string fileContent, string envWorld, string envCharacter)
        {
            string world = "";
            string character = "";

            if (!string.IsNullOrEmpty(fileContent))
            {
                foreach (var rawLine in fileContent.Split('\n'))
                {
                    var line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    var eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    var key = line.Substring(0, eq).Trim();
                    var value = line.Substring(eq + 1).Trim();
                    if (key.Equals("world", StringComparison.OrdinalIgnoreCase)) world = value;
                    else if (key.Equals("character", StringComparison.OrdinalIgnoreCase)) character = value;
                }
            }

            if (string.IsNullOrEmpty(world)) world = (envWorld ?? "").Trim();
            if (string.IsNullOrEmpty(character)) character = (envCharacter ?? "").Trim();
            return (world, character);
        }
    }
}
#endif
