// KeyboardInputProvider.cs — Keyboard-based IInputProvider for Editor/desktop testing.
// Supports steering, throttle, brake, simulated clutch (Left Shift), paddles (Q/E), and manual gears (1-6, 0, R).
using UnityEngine;

namespace AutoSim.Input
{
    /// <summary>
    /// Keyboard input provider for testing in the Unity Editor and desktop mode.
    /// Maps WASD / Arrow keys to driving inputs, LeftShift to Clutch, Q/E to paddles,
    /// and 0..6 / R to the simulated shifter.
    /// </summary>
    [AddComponentMenu("AutoSim/Input/Keyboard Input Provider")]
    public class KeyboardInputProvider : MonoBehaviour, IInputProvider
    {
        // ---------------------------------------------------------------
        //  Tuning Parameters
        // ---------------------------------------------------------------

        [Header("Steering")]
        [Tooltip("How quickly steering ramps toward the target (degrees/sec equivalent).")]
        [SerializeField] private float steeringSensitivity = 3.0f;

        [Tooltip("How quickly steering returns to centre when no key is held.")]
        [SerializeField] private float steeringReturnSpeed = 5.0f;

        [Header("Throttle & Brake")]
        [Tooltip("How quickly throttle ramps from 0→1.")]
        [SerializeField] private float throttleRampSpeed = 3.0f;

        [Tooltip("How quickly throttle falls from 1→0 when released.")]
        [SerializeField] private float throttleDecaySpeed = 4.0f;

        [Tooltip("How quickly brake ramps from 0→1.")]
        [SerializeField] private float brakeRampSpeed = 5.0f;

        [Tooltip("How quickly brake falls from 1→0 when released.")]
        [SerializeField] private float brakeDecaySpeed = 5.0f;

        [Header("Clutch Simulation")]
        [Tooltip("How quickly clutch depresses when LeftShift is held.")]
        [SerializeField] private float clutchRampSpeed = 8.0f;

        // ---------------------------------------------------------------
        //  IInputProvider Implementation
        // ---------------------------------------------------------------

        /// <inheritdoc/>
        public float Steering { get; private set; }

        /// <inheritdoc/>
        public float Throttle { get; private set; }

        /// <inheritdoc/>
        public float Brake { get; private set; }

        /// <inheritdoc/>
        public float Clutch { get; private set; }

        /// <inheritdoc/>
        public uint Buttons { get; private set; }

        /// <inheritdoc/>
        public bool IsConnected => true;

        /// <inheritdoc/>
        public string ProviderName => "Keyboard";

        // ---------------------------------------------------------------
        //  Internal State
        // ---------------------------------------------------------------

        private float targetSteering;
        private float targetThrottle;
        private float targetBrake;
        private float targetClutch;
        private uint activeGearMask = 0;

        // ---------------------------------------------------------------
        //  Unity Lifecycle
        // ---------------------------------------------------------------

        private void Update()
        {
            ReadInputKeys();
            SmoothValues();
        }

        // ---------------------------------------------------------------
        //  Input Reading
        // ---------------------------------------------------------------

        /// <summary>
        /// Reads raw key states and sets target values for smoothing.
        /// </summary>
        private void ReadInputKeys()
        {
            // --- Steering ---
            // D / RightArrow = +1 (right), A / LeftArrow = -1 (left)
            float steerRaw = 0f;
            if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow))
                steerRaw += 1f;
            if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow))
                steerRaw -= 1f;

            targetSteering = steerRaw;

            // --- Throttle ---
            // W / UpArrow
            targetThrottle = (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow))
                ? 1f
                : 0f;

            // --- Brake ---
            // S / DownArrow or Space (handbrake)
            bool braking = UnityEngine.Input.GetKey(KeyCode.S)
                         || UnityEngine.Input.GetKey(KeyCode.DownArrow)
                         || UnityEngine.Input.GetKey(KeyCode.Space);
            targetBrake = braking ? 1f : 0f;

            // --- Clutch ---
            // Left Shift or C key
            targetClutch = (UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.C))
                ? 1f
                : 0f;

            // --- Buttons & Shifter ---
            uint btn = 0;

            // Paddles
            if (UnityEngine.Input.GetKey(KeyCode.Q))
                btn |= AutoSimConstants.BTN_PADDLE_LEFT;
            if (UnityEngine.Input.GetKey(KeyCode.E))
                btn |= AutoSimConstants.BTN_PADDLE_RIGHT;

            // Manual Shifter keys (toggle active gear latch)
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1)) activeGearMask = AutoSimConstants.BTN_GEAR_1;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2)) activeGearMask = AutoSimConstants.BTN_GEAR_2;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3)) activeGearMask = AutoSimConstants.BTN_GEAR_3;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4)) activeGearMask = AutoSimConstants.BTN_GEAR_4;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha5)) activeGearMask = AutoSimConstants.BTN_GEAR_5;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha6)) activeGearMask = AutoSimConstants.BTN_GEAR_6;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.R)) activeGearMask = AutoSimConstants.BTN_GEAR_R;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha0)) activeGearMask = 0;

            btn |= activeGearMask;
            Buttons = btn;
        }

        // ---------------------------------------------------------------
        //  Smoothing
        // ---------------------------------------------------------------

        /// <summary>
        /// Smoothly lerps current values toward their targets for natural feel.
        /// </summary>
        private void SmoothValues()
        {
            float dt = Time.deltaTime;

            // Steering: lerp toward target; faster return to centre
            if (Mathf.Approximately(targetSteering, 0f))
            {
                Steering = Mathf.MoveTowards(Steering, 0f, steeringReturnSpeed * dt);
            }
            else
            {
                Steering = Mathf.MoveTowards(Steering, targetSteering, steeringSensitivity * dt);
            }

            // Throttle: ramp up / decay
            if (targetThrottle > Throttle)
                Throttle = Mathf.MoveTowards(Throttle, targetThrottle, throttleRampSpeed * dt);
            else
                Throttle = Mathf.MoveTowards(Throttle, targetThrottle, throttleDecaySpeed * dt);

            // Brake: ramp up / decay
            if (targetBrake > Brake)
                Brake = Mathf.MoveTowards(Brake, targetBrake, brakeRampSpeed * dt);
            else
                Brake = Mathf.MoveTowards(Brake, targetBrake, brakeDecaySpeed * dt);

            // Clutch: fast response
            Clutch = Mathf.MoveTowards(Clutch, targetClutch, clutchRampSpeed * dt);

            // Clamp all values to valid ranges
            Steering = Mathf.Clamp(Steering, -1f, 1f);
            Throttle = Mathf.Clamp01(Throttle);
            Brake = Mathf.Clamp01(Brake);
            Clutch = Mathf.Clamp01(Clutch);
        }
    }
}
