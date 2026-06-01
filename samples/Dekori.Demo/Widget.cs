namespace Dekori.Demo;

/// <summary>A plain payload type returned by the generic repository sample.</summary>
public sealed class Widget
{
    public string Name { get; set; } = "demo-widget";

    public override string ToString() => Name;
}
