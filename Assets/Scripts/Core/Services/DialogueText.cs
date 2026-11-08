using System.Text.RegularExpressions;

namespace Bush.Core
{
    /// <summary>
    /// Tells speech from narration in a dialogue line, so a creature's voice only plays when they actually say
    /// something. Stage directions go in <c>&lt;i&gt;…&lt;/i&gt;</c>, <c>(…)</c> or <c>*…*</c>. A line that's nothing but
    /// stage directions, like "&lt;i&gt;Gus gives you a friendly nod.&lt;/i&gt;", is silent. One with words as well, like
    /// "&lt;i&gt;(a bite)&lt;/i&gt; I was seven", is spoken.
    /// </summary>
    public static class DialogueText
    {
        private static readonly Regex StageDirections = new(@"<i>.*?</i>|\(.*?\)|\*.*?\*", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        private static readonly Regex Tags = new(@"<[^>]*>");

        public static bool IsSpoken(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            string words = Tags.Replace(StageDirections.Replace(line, ""), "");
            foreach (char c in words)
                if (char.IsLetter(c)) return true;
            return false;
        }

        /// <summary>A line of narration, shown in italics, e.g. "Gus gives you a friendly nod."</summary>
        public static string Narration(string text) => $"<i>{text}</i>";
    }
}
