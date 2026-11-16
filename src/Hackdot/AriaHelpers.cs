namespace Hackdot;

public static class AriaHelpers
{
    /// <summary>
    /// ARIA states need the literal strings "true"/"false". Blazor renders a plain <c>true</c>
    /// as an empty attribute, which assistive technology does not understand.
    /// </summary>
    public static string Aria(bool value) => value ? "true" : "false";
}
