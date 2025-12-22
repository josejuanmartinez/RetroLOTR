using System.Text;

public static class ActionNameUtils
{
    public static string StripShortcut(string actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName)) return string.Empty;

        StringBuilder sb = new();
        for (int i = 0; i < actionName.Length; i++)
        {
            char c = actionName[i];
            if (c == '[')
            {
                int close = actionName.IndexOf(']', i + 1);
                if (close > i)
                {
                    sb.Append(actionName.Substring(i + 1, close - i - 1));
                    i = close;
                    continue;
                }
            }
            sb.Append(c);
        }

        return CollapseSpaces(sb.ToString()).Trim();
    }

    private static string CollapseSpaces(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        StringBuilder sb = new();
        bool lastWasSpace = false;
        foreach (char c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                if (lastWasSpace) continue;
                lastWasSpace = true;
                sb.Append(' ');
            }
            else
            {
                lastWasSpace = false;
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
