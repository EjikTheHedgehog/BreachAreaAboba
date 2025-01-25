using System.Drawing;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace BreachArea;

public class BreachAreaSettings : ISettings
{
    public RenderSettings RenderSettings { get; set; } = new RenderSettings();
    public DebugMenu DebugMenu { get; set; } = new DebugMenu();
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
}

[Submenu(CollapsedByDefault = false)]
public class RenderSettings
{
    public ToggleNode RenderTimer { get; set; } = new ToggleNode(true);
    public RangeNode<int> HowManyBreachesToRender { get; set; } = new RangeNode<int>(5, 1, 15);
    public ToggleNode RenderStaticRange { get; set; } = new ToggleNode(true);
    public ToggleNode HideStaticRangeAfterClear { get; set; } = new ToggleNode(true);
    public ToggleNode RenderActiveRange { get; set; } = new ToggleNode(true);
    public ColorNode StaticBreachColor { get; set; } = new ColorNode(Color.Purple);
    public ColorNode ActiveBreachColor { get; set; } = new ColorNode(Color.White);
    public RangeNode<int> CirclesThickness { get; set; } = new RangeNode<int>(2, 1, 10);
}


[Submenu(CollapsedByDefault = true)]
public class DebugMenu
{
    public ToggleNode ShowDebugMenu { get; set; } = new ToggleNode(false);
    [Menu("Specific Duration (~55 default)")]
    public RangeNode<int> SpecificDuration { get; set; } = new RangeNode<int>(55, 1, 70);
    [Menu("Specific Range (~450 default)")]
    public RangeNode<int> SpecificRange { get; set; } = new RangeNode<int>(450, 1, 600);
}