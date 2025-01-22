using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace BreachArea;

public class BreachAreaSettings : ISettings
{
    //Mandatory setting to allow enabling/disabling your plugin
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    //Put all your settings here if you can.
    //There's a bunch of ready-made setting nodes,
    //nested menu support and even custom callbacks are supported.
    //If you want to override DrawSettings instead, you better have a very good reason.

    public RangeNode<float> MaxCircleSize { get; set; } = new RangeNode<float>(455, 50, 1000);
    public RangeNode<float> CustomScale { get; set; } = new RangeNode<float>(1.0f, 0.1f, 10.0f);
    public RangeNode<float> StaticCircleSize { get; set; } = new RangeNode<float>(455, 50, 1000);
}