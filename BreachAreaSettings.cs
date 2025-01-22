using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace BreachArea;

public class BreachAreaSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
    public RangeNode<float> MaxCircleSize { get; set; } = new RangeNode<float>(455, 50, 1000);
    public RangeNode<float> CustomScale { get; set; } = new RangeNode<float>(1.0f, 0.1f, 10.0f);
    public RangeNode<float> StaticCircleSize { get; set; } = new RangeNode<float>(455, 50, 1000);
}