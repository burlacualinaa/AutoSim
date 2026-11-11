// LogitechG920InputProvider.cs — Reads Logitech G920 steering wheel and pedals on Windows PC.
// Implements IInputProvider for seamless consumption by VehicleController and UDPInputSender.
// Excluded from HoloLens 2 UWP ARM64 builds (stub provided below) to keep PC code separate.
#if !UNITY_WSA || UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using AutoSim.Networking;

namespace AutoSim.Input
{
    /// <summary>
    /// Input provider that reads steering wheel angle, accelerator, and brake pedals from
    /// a physical Logitech G920 (or compatible racing wheel) connected to the Windows PC.
    /// Supports multi-device auto-detection (resolves ghost device instances), dynamic active switching,
    /// deadzones, inversion, calibration, on-screen debug HUD, live axis diagnostics,
    /// and seamless keyboard fallback when the wheel is disconnected.
    /// </summary>
    [AddComponentMenu("AutoSim/Input/Logitech G920 Input Provider")]
    public class LogitechG920InputProvider : MonoBehaviour, IInputProvider
    {
        // ──────────────────────────── Inspector Settings ────────────────────────────

        [Header("Device Detection")]
        [Tooltip("Search string used to match the wheel device name (case-insensitive).")]
        [SerializeField] private string deviceMatchFilter = "G920";

        [Tooltip("If true, automatically falls back to KeyboardInputProvider when wheel is not connected.")]
        [SerializeField] private bool autoFallbackToKeyboard = true;

        [Tooltip("Optional reference to KeyboardInputProvider for fallback.")]
        [SerializeField] private KeyboardInputProvider keyboardFallback;

        [Header("Axis Name Overrides (Optional - leave empty for auto-detection)")]
        [Tooltip("Manual override for steering control path/name (e.g. 'x', 'stick/x').")]
        [SerializeField] private string steerAxisOverride = "";

        [Tooltip("Manual override for throttle control path/name (e.g. 'y', 'stick/y', 'rightTrigger').")]
        [SerializeField] private string throttleAxisOverride = "";

        [Tooltip("Manual override for brake control path/name (e.g. 'z', 'rz', 'leftTrigger').")]
        [SerializeField] private string brakeAxisOverride = "";

        [Tooltip("Manual override for clutch control path/name (e.g. 'rz', 'slider', 'rx').")]
        [SerializeField] private string clutchAxisOverride = "";

        [Header("Steering Calibration")]
        [Tooltip("Invert steering axis.")]
        [SerializeField] private bool invertSteering = false;

        [Tooltip("Steering deadzone around center (-1..+1). Values within this range are treated as zero.")]
        [SerializeField, Range(0f, 0.2f)] private float steeringDeadzone = 0.02f;

        [Tooltip("Steering sensitivity curve exponent (1 = linear, >1 = finer control near center).")]
        [SerializeField, Range(0.5f, 3f)] private float steeringLinearity = 1.0f;

        [Header("Throttle Pedal Calibration")]
        [Tooltip("Invert throttle axis.")]
        [SerializeField] private bool invertThrottle = false;

        [Tooltip("Throttle deadzone (0..0.2). Values below this are treated as 0.")]
        [SerializeField, Range(0f, 0.2f)] private float throttleDeadzone = 0.03f;

        [Tooltip("Raw axis value when throttle is completely unpressed (G920 defaults to -1.0).")]
        [SerializeField] private float throttleRestValue = -1f;

        [Tooltip("Raw axis value when throttle is fully pressed (G920 defaults to +1.0).")]
        [SerializeField] private float throttlePressedValue = 1f;

        [Header("Brake Pedal Calibration")]
        [Tooltip("Invert brake axis.")]
        [SerializeField] private bool invertBrake = false;

        [Tooltip("Brake deadzone (0..0.2). Values below this are treated as 0.")]
        [SerializeField, Range(0f, 0.2f)] private float brakeDeadzone = 0.03f;

        [Tooltip("Raw axis value when brake is completely unpressed (G920 defaults to 1.0).")]
        [SerializeField] private float brakeRestValue = 1f;

        [Tooltip("Raw axis value when brake is fully pressed (G920 defaults to -1.0).")]
        [SerializeField] private float brakePressedValue = -1f;

        [Header("Clutch Pedal")]
        [Tooltip("Enable reading clutch pedal.")]
        [SerializeField] private bool enableClutch = true;

        [SerializeField] private bool invertClutch = false;
        [SerializeField, Range(0f, 0.2f)] private float clutchDeadzone = 0.03f;
        [SerializeField] private float clutchRestValue = 1f;
        [SerializeField] private float clutchPressedValue = -1f;

        [Header("Diagnostics & Debug Display")]
        [Tooltip("Enable detailed diagnostic logging in Unity Console upon device discovery.")]
        [SerializeField] private bool verboseLogging = true;

        [Tooltip("Show on-screen debug interface displaying wheel & pedal values (press F3 to toggle).")]
        [SerializeField] private bool showDebugOverlay = false;

        [Tooltip("Keyboard shortcut to toggle debug overlay.")]
        [SerializeField] private KeyCode toggleDebugKey = KeyCode.F3;

        [Header("HoloLens 2 UDP Streaming")]
        [Tooltip("Stream live wheel & pedal inputs over Wi-Fi UDP to HoloLens 2.")]
        [SerializeField] private bool streamToHoloLens = true;

        [Tooltip("Wi-Fi IP address of the HoloLens 2.")]
        [SerializeField] private string holoLensIP = "192.168.0.153";

        [Tooltip("Target UDP port on HoloLens 2 (default 9050).")]
        [SerializeField] private int holoLensPort = AutoSimConstants.DEFAULT_UDP_PORT;

        // ──────────────────────────── IInputProvider API ────────────────────────────

        /// <summary>Normalised steering: -1 (full left) to +1 (full right).</summary>
        public float Steering { get; private set; }

        /// <summary>Normalised throttle: 0 (idle) to 1 (full throttle).</summary>
        public float Throttle { get; private set; }

        /// <summary>Normalised brake: 0 (released) to 1 (full brake).</summary>
        public float Brake { get; private set; }

        /// <summary>Normalised clutch: 0 (released) to 1 (fully depressed).</summary>
        public float Clutch { get; private set; }

        /// <summary>Active button bitmask (paddles, face buttons).</summary>
        public uint Buttons { get; private set; }

        /// <summary>Active gear detected from Logitech Driving Force H-Shifter (-1=Reverse, 0=Neutral, 1..6=Gears 1-6).</summary>
        public int ShifterPosition { get; private set; }

        /// <summary>Whether Left Paddle (Gear Down / LB) is currently pressed.</summary>
        public bool PaddleLeftPressed { get; private set; }

        /// <summary>Whether Right Paddle (Gear Up / RB) is currently pressed.</summary>
        public bool PaddleRightPressed { get; private set; }

        /// <summary>True if a physical wheel or fallback provider is actively feeding input.</summary>
        public bool IsConnected => IsWheelConnected || (autoFallbackToKeyboard && keyboardFallback != null && keyboardFallback.IsConnected);

        /// <summary>Human-readable provider name for debug UI.</summary>
        public string ProviderName
        {
            get
            {
                if (IsWheelConnected)
                    return $"Logitech G920 ({detectedWheelName})";
                if (autoFallbackToKeyboard && keyboardFallback != null)
                    return "Logitech G920 [Fallback: Keyboard]";
                return "Logitech G920 [Disconnected]";
            }
        }

        // ──────────────────────────── Public State ──────────────────────────────────

        /// <summary>True if a physical Logitech G920 or compatible wheel is detected.</summary>
        public bool IsWheelConnected { get; private set; }

        /// <summary>Device name reported by driver.</summary>
        public string DetectedWheelName => detectedWheelName;

        /// <summary>Raw steering axis value read directly from driver before calibration.</summary>
        public float RawSteering { get; private set; }

        /// <summary>Raw throttle axis value before calibration.</summary>
        public float RawThrottle { get; private set; }

        /// <summary>Raw brake axis value before calibration.</summary>
        public float RawBrake { get; private set; }

        /// <summary>Raw clutch axis value before calibration.</summary>
        public float RawClutch { get; private set; }

        /// <summary>Whether auto-calibration mode is currently running.</summary>
        public bool IsCalibrating => isCalibrating;

        public KeyboardInputProvider KeyboardFallback
        {
            get => keyboardFallback;
            set => keyboardFallback = value;
        }

        // ──────────────────────────── Events ────────────────────────────────────────

        public event Action<string> OnWheelConnected;
        public event Action OnWheelDisconnected;

        // ──────────────────────────── Multi-Device Candidate Struct ─────────────────

        private class WheelCandidate
        {
            public InputDevice Device;
            public string DisplayName;
            public AxisControl SteerControl;
            public AxisControl ThrottleControl;
            public AxisControl BrakeControl;
            public AxisControl ClutchControl;
            public float LastSteer;
            public float LastThrottle;
            public float LastBrake;
            public float LastClutch;
        }

        private readonly List<WheelCandidate> candidates = new List<WheelCandidate>();
        private int activeCandidateIndex = -1;

        // ──────────────────────────── Internal State ────────────────────────────────

        private InputDevice activeWheelDevice;
        private string detectedWheelName = "None";
        private float devicePollTimer = 0f;
        private const float DEVICE_POLL_INTERVAL = 1.0f;

        // Auto-calibration state
        private bool isCalibrating = false;
        private float calibMinThrottle = float.MaxValue;
        private float calibMaxThrottle = float.MinValue;
        private float calibMinBrake = float.MaxValue;
        private float calibMaxBrake = float.MinValue;
        private float calibMinClutch = float.MaxValue;
        private float calibMaxClutch = float.MinValue;

        private System.Net.Sockets.UdpClient netUdpClient;
        private System.Net.IPEndPoint netRemoteEndPoint;
        private uint netPacketSequence = 0;
        private float netSendTimer = 0f;
        private int netPacketsSentCount = 0;

        // Cached controls on the active InputDevice
        private AxisControl steerControl;
        private AxisControl throttleControl;
        private AxisControl brakeControl;
        private AxisControl clutchControl;

        // GUI Styles
        private GUIStyle panelStyle;
        private GUIStyle labelStyle;
        private GUIStyle headerStyle;
        private Texture2D greenTex;
        private Texture2D redTex;
        private Texture2D blueTex;
        private Texture2D orangeTex;
        private Texture2D grayTex;
        private bool guiStylesInit = false;
        private bool showDiagnosticsOverlay = false;

        // ──────────────────────────── Unity Lifecycle ───────────────────────────────

        private void Awake()
        {
            // Ensure OpenXR Dpad collision is resolved prior to device discovery
            OpenXRInputLayoutFixer.ApplyLayoutFix();

            holoLensIP = PlayerPrefs.GetString("AutoSim_HoloLensIP", holoLensIP);

            if (keyboardFallback == null)
                keyboardFallback = GetComponent<KeyboardInputProvider>();

            ScanForWheelDevice();
        }

        private void OnEnable()
        {
            InputSystem.onDeviceChange += OnInputDeviceChanged;
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnInputDeviceChanged;
        }

        private void OnDestroy()
        {
            if (netUdpClient != null)
            {
                try { netUdpClient.Close(); } catch { }
                netUdpClient = null;
            }

            if (greenTex != null) Destroy(greenTex);
            if (redTex != null) Destroy(redTex);
            if (blueTex != null) Destroy(blueTex);
            if (orangeTex != null) Destroy(orangeTex);
            if (grayTex != null) Destroy(grayTex);
        }

        private void Update()
        {
            // Toggle overlay
            if (UnityEngine.Input.GetKeyDown(toggleDebugKey))
                showDebugOverlay = !showDebugOverlay;

            // Periodic device check if not connected
            devicePollTimer += Time.unscaledDeltaTime;
            if (devicePollTimer >= DEVICE_POLL_INTERVAL)
            {
                devicePollTimer = 0f;
                if (!IsWheelConnected || candidates.Count == 0)
                    ScanForWheelDevice();
            }

            // Read physical inputs
            if (IsWheelConnected && candidates.Count > 0)
            {
                ReadWheelInputs();

                // Hybrid input: allow keyboard (WASD / Arrows) to also steer and throttle/brake seamlessly
                if (autoFallbackToKeyboard && keyboardFallback != null)
                {
                    // Steer with keyboard if keyboard keys are held and wheel is near centre
                    if (Mathf.Abs(keyboardFallback.Steering) > 0.05f && Mathf.Abs(Steering) < 0.15f)
                    {
                        Steering = keyboardFallback.Steering;
                    }

                    // Throttle with W key if pressed
                    if (keyboardFallback.Throttle > 0.05f)
                    {
                        Throttle = Mathf.Max(Throttle, keyboardFallback.Throttle);
                    }

                    // Brake with S / Space key if pressed
                    if (keyboardFallback.Brake > 0.05f)
                    {
                        Brake = Mathf.Max(Brake, keyboardFallback.Brake);
                    }
                }
            }
            else
            {
                // Fallback to keyboard
                if (autoFallbackToKeyboard && keyboardFallback != null)
                {
                    Steering = keyboardFallback.Steering;
                    Throttle = keyboardFallback.Throttle;
                    Brake = keyboardFallback.Brake;
                    Clutch = 0f;
                    Buttons = keyboardFallback.Buttons;
                }
                else
                {
                    Steering = 0f;
                    Throttle = 0f;
                    Brake = 0f;
                    Clutch = 0f;
                    Buttons = 0;
                }
            }

            // Stream inputs to HoloLens 2 over UDP
            if (streamToHoloLens)
            {
                StreamInputsToHoloLens();
            }
        }

        private void StreamInputsToHoloLens()
        {
            if (string.IsNullOrEmpty(holoLensIP)) return;

            netSendTimer += Time.unscaledDeltaTime;
            if (netSendTimer >= 1f / 60f) // 60 Hz
            {
                netSendTimer = 0f;
                try
                {
                    if (netUdpClient == null)
                    {
                        netUdpClient = new System.Net.Sockets.UdpClient();
                    }

                    if (netRemoteEndPoint == null || netRemoteEndPoint.Address.ToString() != holoLensIP.Trim())
                    {
                        if (System.Net.IPAddress.TryParse(holoLensIP.Trim(), out var ip))
                        {
                            netRemoteEndPoint = new System.Net.IPEndPoint(ip, holoLensPort);
                        }
                        else
                        {
                            return;
                        }
                    }

                    var packet = new InputDataPacket
                    {
                        sequenceNumber = netPacketSequence++,
                        steering = Steering,
                        throttle = Throttle,
                        brake = Brake,
                        clutch = Clutch,
                        buttons = Buttons,
                        timestamp = Time.realtimeSinceStartup
                    };

                    byte[] data = packet.Serialize();
                    netUdpClient.Send(data, data.Length, netRemoteEndPoint);
                    netPacketsSentCount++;
                }
                catch (Exception)
                {
                    // Ignore transient network errors
                }
            }
        }

        // ──────────────────────────── Device Detection ──────────────────────────────

        private void OnInputDeviceChanged(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)
            {
                if (verboseLogging)
                    Debug.Log($"[LogitechG920InputProvider] Device notification: {change} -> {device.displayName ?? device.name}");
                ScanForWheelDevice();
            }
            else if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected)
            {
                if (device == activeWheelDevice)
                {
                    ScanForWheelDevice();
                }
            }
        }

        /// <summary>
        /// Scans all InputSystem devices for Logitech G920 or racing wheels.
        /// Finds all matching instances to resolve ghost/stale device handles on Windows.
        /// </summary>
        [ContextMenu("Scan For Wheel Device")]
        public void ScanForWheelDevice()
        {
            // Ensure OpenXR layout fix is active
            OpenXRInputLayoutFixer.ApplyLayoutFix();

            candidates.Clear();
            activeCandidateIndex = -1;
            activeWheelDevice = null;
            steerControl = null;
            throttleControl = null;
            brakeControl = null;
            clutchControl = null;

            Debug.Log($"[LogitechG920InputProvider] === SCANNING {InputSystem.devices.Count} REGISTERED DEVICES ===");
            for (int i = 0; i < InputSystem.devices.Count; i++)
            {
                var d = InputSystem.devices[i];
                Debug.Log($"  Device #{i}: '{d.name}' | Display='{d.displayName}' | Layout='{d.layout}' | Type={d.GetType().Name} | Added={d.added} | Enabled={d.enabled}");
            }

            // Find ALL matching candidate devices
            foreach (var device in InputSystem.devices)
            {
                string devName = device.name ?? "";
                string displayName = device.displayName ?? "";
                string product = device.description.product ?? "";
                string manufacturer = device.description.manufacturer ?? "";
                string layout = device.layout ?? "";
                string combined = $"{devName} {displayName} {product} {manufacturer} {layout}".ToLowerInvariant();

                bool isMatch = combined.Contains(deviceMatchFilter.ToLowerInvariant()) ||
                               (combined.Contains("logitech") && (combined.Contains("wheel") || combined.Contains("g920") || combined.Contains("g29") || combined.Contains("driving force"))) ||
                               (device is Joystick && (combined.Contains("wheel") || combined.Contains("driving") || combined.Contains("pedal") || combined.Contains("racing")));

                if (isMatch)
                {
                    try { InputSystem.EnableDevice(device); } catch { }
                    string friendly = displayName.Length > 0 ? displayName : (product.Length > 0 ? product : devName);
                    var cand = CreateCandidate(device, friendly);
                    if (cand != null)
                    {
                        candidates.Add(cand);
                        Debug.Log($"  -> Enrolled Candidate #{candidates.Count - 1}: '{device.name}' ({friendly})");
                    }
                }
            }

            if (candidates.Count > 0)
            {
                // Select the latest candidate by default (e.g. Device #3 over stale Device #2)
                SelectCandidate(candidates.Count - 1);
                return;
            }

            if (IsWheelConnected)
            {
                HandleDeviceDisconnected();
            }
        }

        private WheelCandidate CreateCandidate(InputDevice device, string friendlyName)
        {
            var cand = new WheelCandidate
            {
                Device = device,
                DisplayName = friendlyName,
                SteerControl = FindAxisControl(device, steerAxisOverride, new[] { "stick/x", "x", "steer", "steering", "wheel", "leftstick/x" }),
                ThrottleControl = FindAxisControl(device, throttleAxisOverride, new[] { "stick/y", "y", "throttle", "accelerat", "righttrigger", "gas" }),
                BrakeControl = FindAxisControl(device, brakeAxisOverride, new[] { "z", "brake", "lefttrigger", "rz", "rotation/z" }),
                ClutchControl = FindAxisControl(device, clutchAxisOverride, new[] { "rz", "rotation/z", "clutch", "slider", "slider1", "rx", "rotation/x" })
            };

            // Prevent duplicate axis binding between brake and clutch
            if (cand.BrakeControl == cand.ClutchControl && cand.BrakeControl != null)
            {
                cand.ClutchControl = null;
                foreach (var ctrl in device.allControls)
                {
                    if (ctrl is AxisControl axis && axis != cand.BrakeControl && axis != cand.SteerControl && axis != cand.ThrottleControl)
                    {
                        cand.ClutchControl = axis;
                        break;
                    }
                }
            }

            // Fallback for Gamepad layout
            if (device is Gamepad pad)
            {
                if (cand.SteerControl == null) cand.SteerControl = pad.leftStick.x;
                if (cand.ThrottleControl == null) cand.ThrottleControl = pad.rightTrigger;
                if (cand.BrakeControl == null) cand.BrakeControl = pad.leftTrigger;
            }

            return cand;
        }

        /// <summary>Selects a candidate wheel device by index.</summary>
        public void SelectCandidate(int index)
        {
            if (candidates.Count == 0 || index < 0 || index >= candidates.Count)
                return;

            activeCandidateIndex = index;
            var cand = candidates[index];
            activeWheelDevice = cand.Device;
            detectedWheelName = cand.DisplayName;
            steerControl = cand.SteerControl;
            throttleControl = cand.ThrottleControl;
            brakeControl = cand.BrakeControl;
            clutchControl = cand.ClutchControl;
            IsWheelConnected = true;

            try
            {
                InputSystem.EnableDevice(cand.Device);
                cand.Device.MakeCurrent();
            }
            catch { }

            StringBuilder diag = new StringBuilder();
            diag.AppendLine($"[LogitechG920InputProvider] *** BOUND WHEEL CANDIDATE #{index} ***");
            diag.AppendLine($"   Friendly Name: {cand.DisplayName}");
            diag.AppendLine($"   Device Name:   {cand.Device.name}");
            diag.AppendLine($"   Layout:        {cand.Device.layout}");
            diag.AppendLine($"   Mapped Controls:");
            diag.AppendLine($"     Steering:  {(steerControl != null ? steerControl.path : "NOT FOUND")}");
            diag.AppendLine($"     Throttle:  {(throttleControl != null ? throttleControl.path : "NOT FOUND")} [Rest: {throttleRestValue:F1}, Pressed: {throttlePressedValue:F1}]");
            diag.AppendLine($"     Brake:     {(brakeControl != null ? brakeControl.path : "NOT FOUND")} [Rest: {brakeRestValue:F1}, Pressed: {brakePressedValue:F1}]");
            diag.AppendLine($"     Clutch:    {(clutchControl != null ? clutchControl.path : "NOT FOUND")} [Rest: {clutchRestValue:F1}, Pressed: {clutchPressedValue:F1}]");

            Debug.Log(diag.ToString());

            OnWheelConnected?.Invoke(detectedWheelName);
        }

        /// <summary>Cycles through candidate wheel devices (useful if multiple G920 handles exist).</summary>
        public void CycleCandidateDevice()
        {
            if (candidates.Count <= 1) return;
            int next = (activeCandidateIndex + 1) % candidates.Count;
            SelectCandidate(next);
        }

        private AxisControl FindAxisControl(InputDevice device, string manualOverride, string[] candidateNames)
        {
            // 1. Check manual override if specified
            if (!string.IsNullOrEmpty(manualOverride))
            {
                var overrideCtrl = device.TryGetChildControl<AxisControl>(manualOverride);
                if (overrideCtrl != null) return overrideCtrl;

                foreach (var ctrl in device.allControls)
                {
                    if (ctrl is AxisControl axis && (ctrl.name.Equals(manualOverride, StringComparison.OrdinalIgnoreCase) ||
                                                     ctrl.path.EndsWith(manualOverride, StringComparison.OrdinalIgnoreCase)))
                    {
                        return axis;
                    }
                }
            }

            // 2. Search device controls for exact candidate name match first
            foreach (var candidate in candidateNames)
            {
                foreach (var ctrl in device.allControls)
                {
                    if (ctrl is AxisControl axis)
                    {
                        string cName = ctrl.name.ToLowerInvariant();
                        string cPath = ctrl.path.ToLowerInvariant();

                        if (cName == candidate || cPath.EndsWith("/" + candidate))
                        {
                            return axis;
                        }
                    }
                }
            }

            // 3. Fallback partial string contains match
            foreach (var candidate in candidateNames)
            {
                foreach (var ctrl in device.allControls)
                {
                    if (ctrl is AxisControl axis)
                    {
                        string cName = ctrl.name.ToLowerInvariant();
                        if (cName.Contains(candidate))
                        {
                            return axis;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Swaps the assigned axes between Brake and Clutch immediately.
        /// </summary>
        public void SwapBrakeAndClutch()
        {
            var tempCtrl = brakeControl;
            brakeControl = clutchControl;
            clutchControl = tempCtrl;

            var tempRest = brakeRestValue;
            brakeRestValue = clutchRestValue;
            clutchRestValue = tempRest;

            var tempPressed = brakePressedValue;
            brakePressedValue = clutchPressedValue;
            clutchPressedValue = tempPressed;

            var tempInvert = invertBrake;
            invertBrake = invertClutch;
            invertClutch = tempInvert;

            Debug.Log($"[LogitechG920InputProvider] Swapped Brake and Clutch! Brake now -> {brakeControl?.path ?? "None"}, Clutch now -> {clutchControl?.path ?? "None"}");
        }

        private void HandleDeviceDisconnected()
        {
            IsWheelConnected = false;
            detectedWheelName = "None";
            activeWheelDevice = null;
            steerControl = null;
            throttleControl = null;
            brakeControl = null;
            clutchControl = null;
            activeCandidateIndex = -1;

            Debug.LogWarning("[LogitechG920InputProvider] Wheel disconnected. Reverting to keyboard fallback if available.");
            OnWheelDisconnected?.Invoke();
        }

        // ──────────────────────────── Input Reading & Processing ────────────────────

        private void ReadWheelInputs()
        {
            if (candidates.Count == 0) return;

            // 1. Inspect ALL candidate devices; if another candidate produces active input, auto-switch to it!
            for (int i = 0; i < candidates.Count; i++)
            {
                var cand = candidates[i];
                if (cand.Device == null || !cand.Device.added) continue;

                float s = cand.SteerControl != null ? cand.SteerControl.ReadValue() : 0f;
                float t = cand.ThrottleControl != null ? cand.ThrottleControl.ReadValue() : 0f;
                float b = cand.BrakeControl != null ? cand.BrakeControl.ReadValue() : 0f;
                float c = cand.ClutchControl != null ? cand.ClutchControl.ReadValue() : 0f;

                bool buttonDown = false;
                foreach (var ctrl in cand.Device.allControls)
                {
                    if (ctrl is ButtonControl btn && btn.isPressed)
                    {
                        buttonDown = true;
                        break;
                    }
                }

                bool moved = Mathf.Abs(s - cand.LastSteer) > 0.005f ||
                             Mathf.Abs(t - cand.LastThrottle) > 0.005f ||
                             Mathf.Abs(b - cand.LastBrake) > 0.005f ||
                             Mathf.Abs(c - cand.LastClutch) > 0.005f;

                bool hasOffset = Mathf.Abs(s) > 0.02f || Mathf.Abs(t) > 0.05f || Mathf.Abs(b) > 0.05f || Mathf.Abs(c) > 0.05f;

                cand.LastSteer = s;
                cand.LastThrottle = t;
                cand.LastBrake = b;
                cand.LastClutch = c;

                if ((moved || buttonDown || hasOffset) && i != activeCandidateIndex)
                {
                    Debug.Log($"[LogitechG920InputProvider] >>> Dynamic switch to active candidate #{i} ({cand.Device.name}) <<<");
                    SelectCandidate(i);
                    break;
                }
            }

            // 2. Read Raw Values from active candidate
            RawSteering = steerControl != null ? steerControl.ReadValue() : 0f;
            RawThrottle = throttleControl != null ? throttleControl.ReadValue() : 0f;
            RawBrake = brakeControl != null ? brakeControl.ReadValue() : 0f;
            RawClutch = (enableClutch && clutchControl != null) ? clutchControl.ReadValue() : 0f;

            // Handle auto-calibration tracking
            if (isCalibrating)
            {
                calibMinThrottle = Mathf.Min(calibMinThrottle, RawThrottle);
                calibMaxThrottle = Mathf.Max(calibMaxThrottle, RawThrottle);
                calibMinBrake = Mathf.Min(calibMinBrake, RawBrake);
                calibMaxBrake = Mathf.Max(calibMaxBrake, RawBrake);
                calibMinClutch = Mathf.Min(calibMinClutch, RawClutch);
                calibMaxClutch = Mathf.Max(calibMaxClutch, RawClutch);
            }

            // 3. Process Steering (-1..+1)
            float steer = RawSteering;
            if (invertSteering) steer = -steer;

            if (Mathf.Abs(steer) < steeringDeadzone)
            {
                steer = 0f;
            }
            else
            {
                float sign = Mathf.Sign(steer);
                float magnitude = (Mathf.Abs(steer) - steeringDeadzone) / (1f - steeringDeadzone);

                if (!Mathf.Approximately(steeringLinearity, 1f))
                    magnitude = Mathf.Pow(magnitude, steeringLinearity);

                steer = sign * Mathf.Clamp01(magnitude);
            }
            Steering = Mathf.Clamp(steer, -1f, 1f);

            // 4. Process Throttle, Brake, Clutch
            Throttle = ProcessPedal(RawThrottle, throttleRestValue, throttlePressedValue, invertThrottle, throttleDeadzone);
            Brake = ProcessPedal(RawBrake, brakeRestValue, brakePressedValue, invertBrake, brakeDeadzone);
            Clutch = enableClutch ? ProcessPedal(RawClutch, clutchRestValue, clutchPressedValue, invertClutch, clutchDeadzone) : 0f;

            // 5. Read Buttons
            ReadButtons();
        }

        private float ProcessPedal(float raw, float rest, float pressed, bool invert, float deadzone)
        {
            float range = pressed - rest;
            if (Mathf.Abs(range) < 0.01f)
            {
                // Safety fallback if uncalibrated: default -1..+1
                range = 2f;
                rest = -1f;
                pressed = 1f;
            }

            float norm = (raw - rest) / range;
            if (invert)
                norm = 1f - norm;

            norm = Mathf.Clamp01(norm);

            // Apply deadzone at bottom of pedal travel
            if (norm <= deadzone)
                return 0f;

            return Mathf.Clamp01((norm - deadzone) / (1f - deadzone));
        }

        private void ReadButtons()
        {
            uint mask = 0;
            PaddleLeftPressed = false;
            PaddleRightPressed = false;
            int detectedShifter = 0;

            if (activeWheelDevice == null)
            {
                Buttons = 0;
                ShifterPosition = 0;
                return;
            }

            // Scan button controls on device
            foreach (var control in activeWheelDevice.allControls)
            {
                if (control is ButtonControl btn && btn.isPressed)
                {
                    string n = btn.name.ToLowerInvariant();
                    // Face buttons
                    if (n.Contains("buttonsouth") || n == "a" || n == "0" || n == "trigger") mask |= AutoSimConstants.BTN_A;
                    else if (n.Contains("buttoneast") || n == "b" || n == "1" || n == "button2") mask |= AutoSimConstants.BTN_B;
                    else if (n.Contains("buttonwest") || n == "x" || n == "2" || n == "button3") mask |= AutoSimConstants.BTN_X;
                    else if (n.Contains("buttonnorth") || n == "y" || n == "3" || n == "button4") mask |= AutoSimConstants.BTN_Y;

                    // Paddles: Right Paddle (Gear Up), Left Paddle (Gear Down)
                    else if (n.Contains("rightshoulder") || n.Contains("paddleright") || n == "5" || n == "button5")
                    {
                        mask |= AutoSimConstants.BTN_PADDLE_RIGHT;
                        PaddleRightPressed = true;
                    }
                    else if (n.Contains("leftshoulder") || n.Contains("paddleleft") || n == "6" || n == "button6" || n == "4")
                    {
                        mask |= AutoSimConstants.BTN_PADDLE_LEFT;
                        PaddleLeftPressed = true;
                    }

                    else if (n.Contains("menu") || n.Contains("start") || n == "7" || n == "button8") mask |= AutoSimConstants.BTN_MENU;

                    // Logitech Driving Force H-Shifter positions (Buttons 11 through 18)
                    else if (n == "button11" || n == "11" || n == "button10" || n == "10") { mask |= AutoSimConstants.BTN_GEAR_1; detectedShifter = 1; }
                    else if (n == "button12" || n == "12") { mask |= AutoSimConstants.BTN_GEAR_2; detectedShifter = 2; }
                    else if (n == "button13" || n == "13") { mask |= AutoSimConstants.BTN_GEAR_3; detectedShifter = 3; }
                    else if (n == "button14" || n == "14") { mask |= AutoSimConstants.BTN_GEAR_4; detectedShifter = 4; }
                    else if (n == "button15" || n == "15") { mask |= AutoSimConstants.BTN_GEAR_5; detectedShifter = 5; }
                    else if (n == "button16" || n == "16") { mask |= AutoSimConstants.BTN_GEAR_6; detectedShifter = 6; }
                    else if (n == "button17" || n == "17" || n == "button18" || n == "18") { mask |= AutoSimConstants.BTN_GEAR_R; detectedShifter = -1; }

                    else mask |= 1u; // generic press
                }
            }

            Buttons = mask;
            ShifterPosition = detectedShifter;
        }

        // ──────────────────────────── Calibration Helpers ───────────────────────────

        /// <summary>Starts auto-learning minimum and maximum pedal ranges.</summary>
        public void StartAutoCalibration()
        {
            isCalibrating = true;
            calibMinThrottle = RawThrottle;
            calibMaxThrottle = RawThrottle;
            calibMinBrake = RawBrake;
            calibMaxBrake = RawBrake;
            calibMinClutch = RawClutch;
            calibMaxClutch = RawClutch;
            Debug.Log("[LogitechG920InputProvider] Calibration started. Depress all 3 pedals completely, then click SAVE CALIBRATION.");
        }

        /// <summary>Applies the learned calibration limits.</summary>
        public void FinishAutoCalibration()
        {
            if (!isCalibrating) return;

            // Rest is where pedal was when calibration started, Pressed is the farthest reached
            if (Mathf.Abs(calibMaxThrottle - calibMinThrottle) > 0.1f)
            {
                throttleRestValue = calibMinThrottle;
                throttlePressedValue = calibMaxThrottle;
            }

            if (Mathf.Abs(calibMaxBrake - calibMinBrake) > 0.1f)
            {
                brakeRestValue = calibMinBrake;
                brakePressedValue = calibMaxBrake;
            }

            if (Mathf.Abs(calibMaxClutch - calibMinClutch) > 0.1f)
            {
                clutchRestValue = calibMinClutch;
                clutchPressedValue = calibMaxClutch;
            }

            isCalibrating = false;
            Debug.Log($"[LogitechG920InputProvider] Calibration saved! Throttle: [{throttleRestValue:F2} .. {throttlePressedValue:F2}], " +
                      $"Brake: [{brakeRestValue:F2} .. {brakePressedValue:F2}], Clutch: [{clutchRestValue:F2} .. {clutchPressedValue:F2}]");
        }

        /// <summary>Resets calibration values to standard Logitech G920 hardware defaults.</summary>
        public void ResetCalibration()
        {
            throttleRestValue = -1f;
            throttlePressedValue = 1f;
            invertThrottle = false;

            brakeRestValue = 1f;
            brakePressedValue = -1f;
            invertBrake = false;

            clutchRestValue = 1f;
            clutchPressedValue = -1f;
            invertClutch = false;

            invertSteering = false;
            isCalibrating = false;

            Debug.Log("[LogitechG920InputProvider] Reset calibration to standard Logitech G920 hardware defaults (-1..+1 gas, +1..-1 brake/clutch).");
        }

        /// <summary>Logs full control state of the connected wheel for troubleshooting.</summary>
        [ContextMenu("Log Full Device Diagnostics")]
        public void DumpDeviceDiagnostics()
        {
            if (activeWheelDevice == null)
            {
                Debug.LogWarning("[LogitechG920InputProvider] No wheel device currently bound to dump diagnostics.");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"[LogitechG920InputProvider] Control Diagnostics for: {activeWheelDevice.displayName} ({activeWheelDevice.layout})");
            foreach (var ctrl in activeWheelDevice.allControls)
            {
                object val = null;
                try { val = ctrl.ReadValueAsObject(); } catch { val = "err"; }
                sb.AppendLine($"   {ctrl.path} [{ctrl.layout}] = {val}");
            }
            Debug.Log(sb.ToString());
        }

        /// <summary>Produces a network-ready InputDataPacket containing current calibrated values.</summary>
        public InputDataPacket GetCurrentPacket(uint sequenceNumber = 0)
        {
            return new InputDataPacket
            {
                sequenceNumber = sequenceNumber,
                steering = Steering,
                throttle = Throttle,
                brake = Brake,
                clutch = Clutch,
                buttons = Buttons,
                timestamp = Time.time
            };
        }

        // ──────────────────────────── OnGUI Debug HUD ───────────────────────────────

        private void InitGUIStyles()
        {
            if (guiStylesInit) return;

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = MakeTex(2, 2, new Color(0.08f, 0.08f, 0.12f, 0.90f));

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal
            };
            labelStyle.normal.textColor = Color.white;

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            headerStyle.normal.textColor = new Color(0.4f, 0.8f, 1f);

            greenTex = MakeTex(2, 2, new Color(0.2f, 0.8f, 0.2f, 0.9f));
            redTex = MakeTex(2, 2, new Color(0.9f, 0.25f, 0.2f, 0.9f));
            blueTex = MakeTex(2, 2, new Color(0.2f, 0.6f, 1.0f, 0.9f));
            orangeTex = MakeTex(2, 2, new Color(1.0f, 0.6f, 0.1f, 0.9f));
            grayTex = MakeTex(2, 2, new Color(0.25f, 0.25f, 0.28f, 0.9f));

            guiStylesInit = true;
        }

        private void OnGUI()
        {
            if (!showDebugOverlay) return;

            InitGUIStyles();

            float panelWidth = 360f;
            float panelHeight = showDiagnosticsOverlay ? 560f : (isCalibrating ? 460f : 435f);
            float panelX = Screen.width - panelWidth - 15f;
            float panelY = 15f;

            GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, panelHeight), panelStyle);
            GUILayout.Space(6);

            // Title
            GUILayout.Label("LOGITECH G920 CONTROLLER", headerStyle);
            GUILayout.Space(2);

            // Status Badge
            string statusStr = IsWheelConnected 
                ? $"CONNECTED: #{activeCandidateIndex} {detectedWheelName} ({candidates.Count} wheel(s) found)" 
                : "DISCONNECTED (Using Keyboard)";
            Color statusColor = IsWheelConnected ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.85f, 0.3f);

            GUI.color = statusColor;
            GUILayout.Label(statusStr, labelStyle);
            GUI.color = Color.white;

            GUILayout.Space(6);

            // Steering Gauge (-1 .. +1)
            GUILayout.Label($"Steering: {Steering:+0.00;-0.00;0.00}  (Raw: {RawSteering:+0.00;-0.00;0.00})", labelStyle);
            DrawSteeringBar(Steering);

            GUILayout.Space(4);

            // Throttle Bar (0 .. 1)
            GUILayout.Label($"Accelerator: {Throttle * 100f:F0}%  (Raw: {RawThrottle:F2})", labelStyle);
            DrawProgressBar(Throttle, greenTex);

            GUILayout.Space(4);

            // Brake Bar (0 .. 1)
            GUILayout.Label($"Brake: {Brake * 100f:F0}%  (Raw: {RawBrake:F2})", labelStyle);
            DrawProgressBar(Brake, redTex);

            GUILayout.Space(4);

            // Clutch Bar (0 .. 1)
            GUILayout.Label($"Clutch: {Clutch * 100f:F0}%  (Raw: {RawClutch:F2})", labelStyle);
            DrawProgressBar(Clutch, orangeTex);

            GUILayout.Space(8);

            // Quick Invert Toggles
            GUILayout.BeginHorizontal();
            invertBrake = GUILayout.Toggle(invertBrake, " Invert Brake", GUILayout.Width(110));
            invertThrottle = GUILayout.Toggle(invertThrottle, " Invert Gas", GUILayout.Width(100));
            invertSteering = GUILayout.Toggle(invertSteering, " Invert Wheel", GUILayout.Width(110));
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Calibration / Control buttons
            GUILayout.BeginHorizontal();
            if (!isCalibrating)
            {
                if (candidates.Count > 1)
                {
                    if (GUILayout.Button($"Switch #{activeCandidateIndex}", GUILayout.Height(22), GUILayout.Width(75)))
                        CycleCandidateDevice();
                }

                if (GUILayout.Button("Calibrate", GUILayout.Height(22)))
                    StartAutoCalibration();
                if (GUILayout.Button("Reset", GUILayout.Height(22), GUILayout.Width(48)))
                    ResetCalibration();
                if (GUILayout.Button("Swap B/C", GUILayout.Height(22), GUILayout.Width(70)))
                    SwapBrakeAndClutch();
                if (GUILayout.Button(showDiagnosticsOverlay ? "Hide" : "Diag", GUILayout.Height(22), GUILayout.Width(45)))
                    showDiagnosticsOverlay = !showDiagnosticsOverlay;
            }
            else
            {
                GUI.color = Color.yellow;
                if (GUILayout.Button("SAVE CALIBRATION", GUILayout.Height(24)))
                    FinishAutoCalibration();
                GUI.color = Color.white;
            }
            GUILayout.EndHorizontal();

            if (isCalibrating)
            {
                GUILayout.Space(3);
                GUI.color = Color.yellow;
                GUILayout.Label("Depress all 3 pedals completely, then click SAVE CALIBRATION.", labelStyle);
                GUI.color = Color.white;
            }

            if (showDiagnosticsOverlay)
            {
                GUILayout.Space(6);
                GUILayout.Label("REGISTERED WHEEL CANDIDATES:", headerStyle);
                for (int i = 0; i < candidates.Count; i++)
                {
                    var c = candidates[i];
                    string prefix = (i == activeCandidateIndex) ? ">>> " : "    ";
                    GUILayout.Label($"{prefix}#{i}: {c.Device.name} [S:{c.LastSteer:F2} T:{c.LastThrottle:F2} B:{c.LastBrake:F2}]", labelStyle);
                }
            }

            // HoloLens UDP Streaming section
            GUILayout.Space(6);
            GUILayout.Label("HOLOLENS 2 STREAMING (UDP):", headerStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("IP:", labelStyle, GUILayout.Width(25));
            holoLensIP = GUILayout.TextField(holoLensIP, GUILayout.Width(130));
            streamToHoloLens = GUILayout.Toggle(streamToHoloLens, " Send", GUILayout.Width(60));
            GUILayout.EndHorizontal();

            string netStatus = streamToHoloLens 
                ? $"Status: STREAMING -> {holoLensIP}:{holoLensPort} ({netPacketsSentCount} pkts)" 
                : "Status: STREAMING PAUSED";
            GUI.color = streamToHoloLens ? new Color(0.3f, 1f, 1f) : Color.gray;
            GUILayout.Label(netStatus, labelStyle);
            GUI.color = Color.white;

            GUILayout.Space(4);
            GUILayout.Label("Toggle overlay: [F3] | Fallback: WASD", labelStyle);

            GUILayout.EndArea();
        }

        private void DrawProgressBar(float value, Texture2D fillTex)
        {
            Rect r = GUILayoutUtility.GetRect(300f, 12f);
            GUI.DrawTexture(r, grayTex);

            float fillWidth = Mathf.Clamp01(value) * r.width;
            if (fillWidth > 0f)
            {
                Rect fillRect = new Rect(r.x, r.y, fillWidth, r.height);
                GUI.DrawTexture(fillRect, fillTex);
            }
        }

        private void DrawSteeringBar(float steerValue)
        {
            Rect r = GUILayoutUtility.GetRect(300f, 12f);
            GUI.DrawTexture(r, grayTex);

            // Center marker
            float centerX = r.x + r.width * 0.5f;
            Rect centerMark = new Rect(centerX - 1f, r.y, 2f, r.height);
            GUI.DrawTexture(centerMark, Texture2D.whiteTexture);

            // Steer bar extending from center
            float barWidth = Mathf.Abs(steerValue) * (r.width * 0.5f);
            Rect barRect;
            if (steerValue >= 0f)
                barRect = new Rect(centerX, r.y, barWidth, r.height);
            else
                barRect = new Rect(centerX - barWidth, r.y, barWidth, r.height);

            GUI.DrawTexture(barRect, blueTex);
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}

#else

// ─────────────────────────────────────────────────────────────────────────────
// UWP ARM64 Stub for HoloLens 2 Builds (Separate PC input from HoloLens)
// ─────────────────────────────────────────────────────────────────────────────
namespace AutoSim.Input
{
    /// <summary>
    /// Lightweight stub for HoloLens 2 UWP ARM64 builds.
    /// Physical Logitech G920 USB wheels are connected to the Windows PC, not HoloLens.
    /// Input is received over UDP via NetworkInputProvider on HoloLens 2.
    /// </summary>
    [UnityEngine.AddComponentMenu("AutoSim/Input/Logitech G920 Input Provider (Stub)")]
    public class LogitechG920InputProvider : UnityEngine.MonoBehaviour, IInputProvider
    {
        public float Steering => 0f;
        public float Throttle => 0f;
        public float Brake => 0f;
        public float Clutch => 0f;
        public uint Buttons => 0;
        public bool IsConnected => false;
        public string ProviderName => "Logitech G920 (Disabled on HoloLens 2 UWP)";
    }
}

#endif
