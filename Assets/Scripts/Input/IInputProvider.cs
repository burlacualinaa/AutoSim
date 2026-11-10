// IInputProvider.cs — Shared input interface for all input sources
// (keyboard, network/G920). VehicleController consumes this.
namespace AutoSim.Input
{
    public interface IInputProvider
    {
        /// <summary>Steering angle normalized: -1 (full left) to +1 (full right)</summary>
        float Steering { get; }
        /// <summary>Throttle 0..1</summary>
        float Throttle { get; }
        /// <summary>Brake 0..1</summary>
        float Brake { get; }
        /// <summary>Clutch 0..1 (3-pedal sets)</summary>
        float Clutch { get; }
        /// <summary>Button bitmask (G920 buttons, paddle shifters, etc.)</summary>
        uint Buttons { get; }
        /// <summary>Whether this provider is actively receiving data</summary>
        bool IsConnected { get; }
        /// <summary>Human-readable name for debug UI</summary>
        string ProviderName { get; }
    }
}
