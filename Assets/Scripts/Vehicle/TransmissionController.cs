// TransmissionController.cs — Dual-mode transmission (Automatic & Manual) for Logitech G920 & AutoSim.
// Manages gear states, paddle shifting (R-N-D), H-shifter with clutch interlock (80%), and key M toggle.
using UnityEngine;
using AutoSim.Input;

namespace AutoSim.Vehicle
{
    public enum TransmissionMode
    {
        Automatic,
        Manual
    }

    public enum AutoSelector
    {
        R, // Reverse
        N, // Neutral
        D  // Drive (gears 1-6 automatically managed)
    }

    /// <summary>
    /// Dual-mode transmission system supporting Automatic (Paddles R-N-D) and Manual (H-Shifter + Clutch).
    /// Enforces clutch interlock (>= 80% depression), speed check on Reverse, and key M toggle.
    /// </summary>
    [AddComponentMenu("AutoSim/Vehicle/Transmission Controller")]
    public class TransmissionController : MonoBehaviour
    {
        // ──────────────────────────── Serialized Settings ─────────────────────────────

        [Header("References")]
        [SerializeField] private VehicleController vehicleController;

        [Header("Mode & State")]
        [Tooltip("Active transmission mode. Toggle with key [M].")]
        [SerializeField] private TransmissionMode mode = TransmissionMode.Automatic;

        [Tooltip("Selector state in Automatic mode (R, N, D).")]
        [SerializeField] private AutoSelector autoSelector = AutoSelector.D;

        [Tooltip("Currently engaged gear: -1 = Reverse, 0 = Neutral, 1..6 = Forward Gears.")]
        [SerializeField] private int currentGear = 1;

        [Header("Clutch Requirements")]
        [Tooltip("Minimum clutch pedal depression fraction (0..1) required to accept manual gear shift.")]
        [Range(0.5f, 0.95f)]
        [SerializeField] private float clutchEngageThreshold = AutoSimConstants.CLUTCH_ENGAGE_THRESHOLD; // 0.80f

        [Header("Safety Speed Limits")]
        [Tooltip("Maximum vehicle speed (km/h) at which shifting into or out of Reverse is permitted.")]
        [SerializeField] private float maxReverseShiftSpeedKmh = 3.0f;

        [Header("Automatic Shift Speeds (km/h)")]
        [SerializeField] private float shift1To2 = 22f;
        [SerializeField] private float shift2To3 = 42f;
        [SerializeField] private float shift3To4 = 68f;
        [SerializeField] private float shift4To5 = 98f;
        [SerializeField] private float shift5To6 = 130f;

        [Header("Automatic Downshift Speeds (km/h with hysteresis)")]
        [SerializeField] private float downshift2To1 = 16f;
        [SerializeField] private float downshift3To2 = 34f;
        [SerializeField] private float downshift4To3 = 58f;
        [SerializeField] private float downshift5To4 = 86f;
        [SerializeField] private float downshift6To5 = 115f;

        // ──────────────────────────── Public Properties ───────────────────────────────

        public TransmissionMode Mode => mode;
        public AutoSelector CurrentAutoSelector => autoSelector;
        public int CurrentGear => currentGear;

        /// <summary>Raw physical gear reported by shifter (-1=R, 0=N, 1..6=Gears).</summary>
        public int PhysicalShifterGear { get; private set; } = 0;

        /// <summary>True when the most recent manual shift attempt was rejected.</summary>
        public bool IsShiftRejected { get; private set; } = false;

        /// <summary>Reason why shift was rejected (e.g. "PRESS CLUTCH", "STOP TO ENGAGE REVERSE").</summary>
        public string ShiftRejectReason { get; private set; } = "";

        /// <summary>Timer in seconds for how long the rejection alert stays visible.</summary>
        public float ShiftRejectTimer { get; private set; } = 0f;

        /// <summary>Flash timer triggered upon successful gear shift.</summary>
        public float GearChangeFlashTimer { get; private set; } = 0f;

        /// <summary>Current effective torque multiplier for the active gear.</summary>
        public float GearRatioMultiplier
        {
            get
            {
                switch (currentGear)
                {
                    case -1: return 3.5f;  // Reverse
                    case 0:  return 0.0f;  // Neutral
                    case 1:  return 4.0f;  // 1st: strong, progressive launch torque
                    case 2:  return 2.6f;  // 2nd: progressive power band
                    case 3:  return 1.8f;  // 3rd: responsive mid-range
                    case 4:  return 1.3f;  // 4th
                    case 5:  return 1.0f;  // 5th
                    case 6:  return 0.8f;  // 6th
                    default: return 1.0f;
                }
            }
        }

        // ──────────────────────────── Internal Tracking ───────────────────────────────

        private int lastPhysicalGear = 0;
        private int rejectedPhysicalGear = 0; // Remembers rejected gear so holding it won't engage later
        private bool lastPaddleLeft = false;
        private bool lastPaddleRight = false;
        private float autoShiftDelayTimer = 0f;

        // ──────────────────────────── Unity Lifecycle ─────────────────────────────────

        private void Awake()
        {
            if (vehicleController == null)
                vehicleController = GetComponent<VehicleController>();
        }

        private void Start()
        {
            if (vehicleController == null)
                vehicleController = FindObjectOfType<VehicleController>();

            // Initialize default gear based on starting mode
            if (mode == TransmissionMode.Automatic)
            {
                autoSelector = AutoSelector.D;
                currentGear = 1;
            }
        }

        private void Update()
        {
            // 1. Handle Mode Toggle Key [M]
            if (UnityEngine.Input.GetKeyDown(KeyCode.M))
            {
                ToggleTransmissionMode();
            }

            // 2. Decrement UI alert timers
            if (ShiftRejectTimer > 0f)
            {
                ShiftRejectTimer -= Time.deltaTime;
                if (ShiftRejectTimer <= 0f)
                {
                    IsShiftRejected = false;
                    ShiftRejectReason = "";
                }
            }

            if (GearChangeFlashTimer > 0f)
            {
                GearChangeFlashTimer -= Time.deltaTime;
            }

            // 3. Update active transmission mode logic
            var input = vehicleController != null ? vehicleController.ActiveInputProvider : null;

            if (mode == TransmissionMode.Automatic)
            {
                UpdateAutomaticMode(input);
            }
            else
            {
                UpdateManualMode(input);
            }
        }

        // ──────────────────────────── Mode Switching [M] ──────────────────────────────

        /// <summary>
        /// Toggles between Automatic and Manual transmission modes with safe gear transitions.
        /// </summary>
        public void ToggleTransmissionMode()
        {
            if (mode == TransmissionMode.Automatic)
            {
                // Switching from Automatic to Manual
                mode = TransmissionMode.Manual;
                if (autoSelector == AutoSelector.R)
                {
                    currentGear = -1;
                }
                else if (autoSelector == AutoSelector.N)
                {
                    currentGear = 0;
                }
                // If in D, keep currently engaged forward gear (1..6)
                ClearShiftRejection();
                Debug.Log($"[Transmission] Switched to MANUAL mode. Active Gear: {currentGear}");
            }
            else
            {
                // Switching from Manual to Automatic
                mode = TransmissionMode.Automatic;
                if (currentGear == -1)
                {
                    autoSelector = AutoSelector.R;
                }
                else if (currentGear == 0)
                {
                    autoSelector = AutoSelector.N;
                }
                else
                {
                    autoSelector = AutoSelector.D;
                }
                ClearShiftRejection();
                Debug.Log($"[Transmission] Switched to AUTOMATIC mode. Selector: {autoSelector}, Gear: {currentGear}");
            }
        }

        // ──────────────────────────── Automatic Mode ──────────────────────────────────

        /// <summary>
        /// Processes paddle shifters and automatic forward gear scheduling.
        /// Paddles follow strict R <-> N <-> D logic.
        /// </summary>
        private void UpdateAutomaticMode(IInputProvider input)
        {
            float speedKmh = vehicleController != null ? vehicleController.SpeedKmh : 0f;

            // Read paddle inputs (hardware bitmask, specific properties, or keyboard E / Q fallback)
            bool paddleLeft = false;
            bool paddleRight = false;

            if (input != null)
            {
                paddleLeft = (input.Buttons & AutoSimConstants.BTN_PADDLE_LEFT) != 0;
                paddleRight = (input.Buttons & AutoSimConstants.BTN_PADDLE_RIGHT) != 0;

                if (input is LogitechG920InputProvider g920)
                {
                    paddleLeft |= g920.PaddleLeftPressed;
                    paddleRight |= g920.PaddleRightPressed;
                }
            }

            // Keyboard fallbacks for desktop testing: E = Right paddle (Gear Up), Q = Left paddle (Gear Down)
            paddleRight |= UnityEngine.Input.GetKeyDown(KeyCode.E);
            paddleLeft |= UnityEngine.Input.GetKeyDown(KeyCode.Q);

            // Edge-triggered paddle presses
            bool paddleRightDown = paddleRight && !lastPaddleRight;
            bool paddleLeftDown = paddleLeft && !lastPaddleLeft;
            lastPaddleRight = paddleRight;
            lastPaddleLeft = paddleLeft;

            // Process Right Paddle (Gear Up / Move towards D)
            if (paddleRightDown)
            {
                switch (autoSelector)
                {
                    case AutoSelector.R:
                        if (speedKmh <= maxReverseShiftSpeedKmh)
                        {
                            autoSelector = AutoSelector.N;
                            currentGear = 0;
                            TriggerGearFlash();
                        }
                        else
                        {
                            RejectShift("STOP VEHICLE TO SHIFT OUT OF R");
                        }
                        break;

                    case AutoSelector.N:
                        autoSelector = AutoSelector.D;
                        currentGear = 1;
                        TriggerGearFlash();
                        break;

                    case AutoSelector.D:
                        // Already in D — paddles do NOT change 1..6 manually in D per specification
                        break;
                }
            }

            // Process Left Paddle (Gear Down / Move towards R)
            if (paddleLeftDown)
            {
                switch (autoSelector)
                {
                    case AutoSelector.D:
                        autoSelector = AutoSelector.N;
                        currentGear = 0;
                        TriggerGearFlash();
                        break;

                    case AutoSelector.N:
                        if (speedKmh <= maxReverseShiftSpeedKmh)
                        {
                            autoSelector = AutoSelector.R;
                            currentGear = -1;
                            TriggerGearFlash();
                        }
                        else
                        {
                            RejectShift("STOP VEHICLE TO ENGAGE R");
                        }
                        break;

                    case AutoSelector.R:
                        // Already in R
                        break;
                }
            }

            // Automatic gear schedule when in D
            if (autoSelector == AutoSelector.D)
            {
                autoShiftDelayTimer -= Time.deltaTime;
                if (autoShiftDelayTimer <= 0f)
                {
                    int targetGear = ComputeAutomaticGear(speedKmh, currentGear);
                    if (targetGear != currentGear)
                    {
                        currentGear = targetGear;
                        autoShiftDelayTimer = 0.4f; // Prevent rapid hunting
                    }
                }
            }
            else if (autoSelector == AutoSelector.N)
            {
                currentGear = 0;
            }
            else if (autoSelector == AutoSelector.R)
            {
                currentGear = -1;
            }
        }

        private int ComputeAutomaticGear(float speedKmh, int current)
        {
            // Upshift thresholds
            if (current == 1 && speedKmh > shift1To2) return 2;
            if (current == 2 && speedKmh > shift2To3) return 3;
            if (current == 3 && speedKmh > shift3To4) return 4;
            if (current == 4 && speedKmh > shift4To5) return 5;
            if (current == 5 && speedKmh > shift5To6) return 6;

            // Downshift thresholds with hysteresis
            if (current == 6 && speedKmh < downshift6To5) return 5;
            if (current == 5 && speedKmh < downshift5To4) return 4;
            if (current == 4 && speedKmh < downshift4To3) return 3;
            if (current == 3 && speedKmh < downshift3To2) return 2;
            if (current == 2 && speedKmh < downshift2To1) return 1;

            return Mathf.Clamp(current, 1, 6);
        }

        // ──────────────────────────── Manual Mode ─────────────────────────────────────

        /// <summary>
        /// Processes Logitech H-Shifter and clutch requirements.
        /// Accepts shifts ONLY if clutch is depressed >= 80%.
        /// </summary>
        private void UpdateManualMode(IInputProvider input)
        {
            float speedKmh = vehicleController != null ? vehicleController.SpeedKmh : 0f;
            float clutch = input != null ? input.Clutch : 0f;

            // Keyboard clutch helper: LeftShift acts as full clutch depression
            if (UnityEngine.Input.GetKey(KeyCode.LeftShift))
                clutch = 1.0f;

            // Read physical shifter position
            int physicalGear = ReadPhysicalShifter(input);
            PhysicalShifterGear = physicalGear;

            // Detect shifter motion
            if (physicalGear != lastPhysicalGear)
            {
                int newPhysicalGear = physicalGear;
                lastPhysicalGear = physicalGear;

                if (newPhysicalGear == 0)
                {
                    // Shifter moved to Neutral: always accepted immediately per manual transmission mechanics
                    currentGear = 0;
                    rejectedPhysicalGear = 0;
                    ClearShiftRejection();
                    TriggerGearFlash();
                }
                else
                {
                    // Shifter moved into a gear (1..6 or -1)
                    bool clutchEngaged = clutch >= clutchEngageThreshold;

                    if (!clutchEngaged)
                    {
                        // Shift attempted WITHOUT clutch -> REJECT!
                        rejectedPhysicalGear = newPhysicalGear;
                        RejectShift("PRESS CLUTCH");
                    }
                    else
                    {
                        // Clutch is pressed >= 80%
                        if (newPhysicalGear == -1 && speedKmh > maxReverseShiftSpeedKmh)
                        {
                            // Vehicle moving forward too fast to engage Reverse
                            rejectedPhysicalGear = newPhysicalGear;
                            RejectShift("STOP TO ENGAGE REVERSE");
                        }
                        else
                        {
                            // Valid shift accepted!
                            currentGear = newPhysicalGear;
                            rejectedPhysicalGear = 0;
                            ClearShiftRejection();
                            TriggerGearFlash();
                        }
                    }
                }
            }
            else
            {
                // Shifter stayed in the same position.
                // Strict rule: If this gear was previously rejected, do NOT engage it later even if clutch is depressed now!
                // The driver must return to Neutral and perform a clean shift.
            }
        }

        /// <summary>
        /// Reads physical gear selection from Logitech G920 hardware buttons or keyboard fallback.
        /// </summary>
        private int ReadPhysicalShifter(IInputProvider input)
        {
            if (input is LogitechG920InputProvider g920 && g920.IsConnected)
            {
                return g920.ShifterPosition;
            }

            if (input != null && input.Buttons != 0)
            {
                uint b = input.Buttons;
                if ((b & AutoSimConstants.BTN_GEAR_1) != 0) return 1;
                if ((b & AutoSimConstants.BTN_GEAR_2) != 0) return 2;
                if ((b & AutoSimConstants.BTN_GEAR_3) != 0) return 3;
                if ((b & AutoSimConstants.BTN_GEAR_4) != 0) return 4;
                if ((b & AutoSimConstants.BTN_GEAR_5) != 0) return 5;
                if ((b & AutoSimConstants.BTN_GEAR_6) != 0) return 6;
                if ((b & AutoSimConstants.BTN_GEAR_R) != 0) return -1;
            }

            // Keyboard direct gear selection for Editor testing (keys 1-6, 0 for N, R for Reverse)
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1)) return 1;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2)) return 2;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3)) return 3;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4)) return 4;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha5)) return 5;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha6)) return 6;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha0)) return 0;
            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) return -1;

            return lastPhysicalGear;
        }

        // ──────────────────────────── Helper Methods ──────────────────────────────────

        private void RejectShift(string reason)
        {
            IsShiftRejected = true;
            ShiftRejectReason = reason;
            ShiftRejectTimer = 2.0f;
            Debug.LogWarning($"[Transmission] Shift Rejected: {reason}");
        }

        private void ClearShiftRejection()
        {
            IsShiftRejected = false;
            ShiftRejectReason = "";
            ShiftRejectTimer = 0f;
        }

        private void TriggerGearFlash()
        {
            GearChangeFlashTimer = 1.0f;
        }
    }
}
