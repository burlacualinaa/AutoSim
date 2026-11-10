// AutoSimConstants.cs — Shared constants used across all AutoSim subsystems
namespace AutoSim
{
    public static class AutoSimConstants
    {
        // Networking
        public const int DEFAULT_UDP_PORT = 9050;
        public const float SEND_RATE_HZ = 60f;
        public const float CONNECTION_TIMEOUT_SECONDS = 3f;

        // Vehicle
        public const float MAX_STEERING_ANGLE = 35f;       // degrees at wheels
        public const float MAX_MOTOR_TORQUE = 400f;         // Nm
        public const float MAX_BRAKE_TORQUE = 3000f;         // Nm
        public const float VEHICLE_MASS = 1500f;             // kg
        public const float MAX_SPEED_KMH = 180f;

        // Telemetry
        public const float TELEMETRY_RECORD_HZ = 60f;
        public const float TELEMETRY_FLUSH_INTERVAL = 5f;   // seconds

        // G920 Button Bitmask
        public const uint BTN_A = 1 << 0;
        public const uint BTN_B = 1 << 1;
        public const uint BTN_X = 1 << 2;
        public const uint BTN_Y = 1 << 3;
        public const uint BTN_RSB = 1 << 4;
        public const uint BTN_LSB = 1 << 5;
        public const uint BTN_XBOX = 1 << 6;
        public const uint BTN_MENU = 1 << 7;
        public const uint BTN_PADDLE_LEFT = 1 << 8;
        public const uint BTN_PADDLE_RIGHT = 1 << 9;

        // Scenario
        public const float HAZARD_TRIGGER_DISTANCE = 80f;   // metres
        public const float PEDESTRIAN_WALK_SPEED = 1.4f;    // m/s
    }
}
