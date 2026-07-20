using System.Linq;

namespace Santana
{
    public static class Namecheck
    {
        public static bool IsNameValid(string name, bool allowSpace = false)
        {
            if (name.StartsWith("["))
                return false;
            if (name.Contains("GM") || name.Contains("GS"))
                return false;

            if (allowSpace)
            {
                if (name.ToLower().Contains("admin"))
                    return false;

                return name.All(ch => char.IsLetterOrDigit(ch) || ch == ' ' || ch == '_');
            }

            return !name.Contains("Ambiente");
        }

        public static bool IsClanNameValid(string name)
        {
            var everyCharAllowed = name.All(ch =>
                char.IsLetterOrDigit(ch) || ch == '_' || ch == '.' || ch == '-'
                || ch == '*' || ch == '[' || ch == ']' || ch == ' ');

            return !everyCharAllowed;
        }
    }
}
