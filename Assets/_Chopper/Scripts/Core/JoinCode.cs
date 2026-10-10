using System.Text;

namespace Chopper.Core
{
    // The short code a host reads out to friends so they can join the world. Six characters from an alphabet with no
    // look-alikes (no 0, O, 1 or I), so it survives being read over voice chat or typed from a screenshot.
    public static class JoinCode
    {
        public const int Length = 6;
        public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static string Generate(System.Random random)
        {
            var code = new StringBuilder(Length);
            for (int i = 0; i < Length; i++) code.Append(Alphabet[random.Next(Alphabet.Length)]);
            return code.ToString();
        }

        // What a player typed, cleaned up: upper case, with spaces and dashes dropped.
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var code = new StringBuilder(text.Length);
            foreach (char c in text.ToUpperInvariant())
            {
                if (c == ' ' || c == '-') continue;
                code.Append(c);
            }
            return code.ToString();
        }

        public static bool IsValid(string code)
        {
            if (code == null || code.Length != Length) return false;
            foreach (char c in code)
            {
                if (Alphabet.IndexOf(c) < 0) return false;
            }
            return true;
        }
    }
}
