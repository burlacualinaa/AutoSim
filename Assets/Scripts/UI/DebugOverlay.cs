// DebugOverlay.cs — Top-left debug HUD showing FPS, input, network, and tracking status.
// Toggle with F2. Uses OnGUI() for zero-dependency rendering.
using UnityEngine;
using AutoSim.Input;
using AutoSim.Networking;
using AutoSim.Vehicle;

namespace AutoSim.UI
{
    /// <summary>
    /// Immediate-mode GUI debug overlay displayed in the top-left corner.
    /// Shows FPS, active input provider info, network stats, eye tracking status,
    /// and telemetry recording status. Toggle with F2.
    /// </summary>
    [AddComponentMenu("AutoSim/UI/Debug Overlay")]
    public class DebugOverlay : MonoBehaviour
    {
        // ---------------------------------------------------------------
        //  Serialized References
        // ---------------------------------------------------------------

        [Header("References (auto-found if empty)")]
        [Tooltip("VehicleController to read active input provider from.")]
        [SerializeField] private VehicleController vehicleController;

        [Tooltip("UDP receiver for network stats.")]
        [SerializeField] private UDPInputReceiver udpReceiver;

        [Header("Display Settings")]
        [Tooltip("Overlay panel width in pixels.")]
        [SerializeField] private float panelWidth = 320f;

        // ---------------------------------------------------------------
        //  Internal State
        // ---------------------------------------------------------------

        private bool isVisible = true;

        // FPS calculation (rolling average)
        private float fpsAccumulator;
        private int fpsFrameCount;
        private float fpsDisplayValue;
        private float fpsUpdateInterval = 0.5f; // Update FPS readout every 0.5s
        private float fpsTimer;

        // Network packets/sec tracking
        private int lastPacketCount;
        private float packetRateTimer;
        private float packetsPerSecond;

        // Cached styles
        private GUIStyle headerStyle;
        private GUIStyle valueStyle;
        private GUIStyle boxStyle;
        private bool stylesInitialised;

        // ---------------------------------------------------------------
        //  Unity Lifecycle
        // ---------------------------------------------------------------

        private void Start()
        {
            if (vehicleController == null)
                vehicleController = FindObjectOfType<VehicleController>();

            if (udpReceiver == null)
                udpReceiver = FindObjectOfType<UDPInputReceiver>();
        }

        private void Update()
        {
            // Toggle with F2
            if (UnityEngine.Input.GetKeyDown(KeyCode.F2))
                isVisible = !isVisible;

            UpdateFps();
            UpdatePacketRate();
        }

        // ---------------------------------------------------------------
        //  FPS Calculation
        // ---------------------------------------------------------------

        /// <summary>
        /// Calculates a rolling-average FPS updated at fixed intervals.
        /// </summary>
        private void UpdateFps()
        {
            fpsAccumulator += Time.unscaledDeltaTime;
            fpsFrameCount++;
            fpsTimer += Time.unscaledDeltaTime;

            if (fpsTimer >= fpsUpdateInterval)
            {
                fpsDisplayValue = fpsFrameCount / fpsAccumulator;
                fpsAccumulator = 0f;
                fpsFrameCount = 0;
                fpsTimer = 0f;
            }
        }

        // ---------------------------------------------------------------
        //  Packet Rate Calculation
        // ---------------------------------------------------------------

        private void UpdatePacketRate()
        {
            packetRateTimer += Time.unscaledDeltaTime;

            if (packetRateTimer >= 1f)
            {
                int currentCount = udpReceiver != null ? udpReceiver.TotalPacketsReceived : 0;
                packetsPerSecond = currentCount - lastPacketCount;
                lastPacketCount = currentCount;
                packetRateTimer = 0f;
            }
        }

        // ---------------------------------------------------------------
        //  OnGUI Rendering
        // ---------------------------------------------------------------

        private void OnGUI()
        {
            if (!isVisible)
                return;

            EnsureStyles();

            // Panel in top-left
            float panelHeight = 300f;
            Rect panelRect = new Rect(10f, 10f, panelWidth, panelHeight);
            GUI.Box(panelRect, "", boxStyle);

            GUILayout.BeginArea(new Rect(18f, 14f, panelWidth - 16f, panelHeight - 20f));

            // --- FPS ---
            Color fpsColour = fpsDisplayValue >= 55f ? Color.green :
                              fpsDisplayValue >= 30f ? Color.yellow : Color.red;
            DrawLine("FPS", $"{fpsDisplayValue:F1}", fpsColour);

            GUILayout.Space(6f);

            // --- Input Provider ---
            DrawHeader("INPUT");
            IInputProvider provider = vehicleController != null ? vehicleController.ActiveInputProvider : null;
            string providerName = provider != null ? provider.ProviderName : "None";
            bool connected = provider != null && provider.IsConnected;

            DrawLine("Provider", providerName, Color.white);
            DrawLine("Connected", connected ? "YES" : "NO", connected ? Color.green : Color.red);

            if (provider != null)
            {
                DrawLine("Steering", $"{provider.Steering:F2}", Color.white);
                DrawLine("Throttle", $"{provider.Throttle:F2}", Color.white);
                DrawLine("Brake", $"{provider.Brake:F2}", Color.white);
                DrawLine("Buttons", $"0x{provider.Buttons:X4}", Color.white);
            }

            GUILayout.Space(6f);

            // --- Network ---
            DrawHeader("NETWORK");
            DrawLine("Packets/sec", $"{packetsPerSecond:F0}", Color.white);
            int totalPkts = udpReceiver != null ? udpReceiver.TotalPacketsReceived : 0;
            DrawLine("Total Rx", $"{totalPkts}", Color.white);

            GUILayout.Space(6f);

            // --- Eye Tracking ---
            DrawHeader("EYE TRACKING");
            // Eye tracking status would come from the MRTK/OpenXR eye tracker.
            // We check for gaze availability via the InputSystem if available.
            bool gazeAvailable = IsGazeAvailable();
            DrawLine("Gaze Status", gazeAvailable ? "Valid" : "Invalid",
                     gazeAvailable ? Color.green : Color.yellow);

            GUILayout.Space(6f);

            // --- Telemetry ---
            DrawHeader("TELEMETRY");
            // Telemetry recording status is read from a TelemetryRecorder if present.
            // Since TelemetryRecorder may not exist yet, show a placeholder.
            DrawLine("Status", "Idle", Color.gray);
            DrawLine("File", "-", Color.gray);
            DrawLine("Frames", "0", Color.gray);

            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------
        //  Drawing Helpers
        // ---------------------------------------------------------------

        private void DrawHeader(string text)
        {
            GUILayout.Label(text, headerStyle);
        }

        private void DrawLine(string label, string value, Color valueColour)
        {
            GUILayout.BeginHorizontal();
            valueStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            GUILayout.Label(label + ":", valueStyle, GUILayout.Width(100f));
            valueStyle.normal.textColor = valueColour;
            GUILayout.Label(value, valueStyle);
            GUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------
        //  Eye Tracking Helper
        // ---------------------------------------------------------------

        /// <summary>
        /// Checks if eye gaze data is available via the Unity XR input subsystem.
        /// Returns false gracefully if no eye tracker is present (e.g. in Editor).
        /// </summary>
        private bool IsGazeAvailable()
        {
#if UNITY_2019_3_OR_NEWER
            var eyeDevices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
                UnityEngine.XR.InputDeviceCharacteristics.EyeTracking, eyeDevices);
            return eyeDevices.Count > 0;
#else
            return false;
#endif
        }

        // ---------------------------------------------------------------
        //  Style Initialisation
        // ---------------------------------------------------------------

        private void EnsureStyles()
        {
            if (stylesInitialised)
                return;

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            headerStyle.normal.textColor = Color.cyan;

            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft
            };

            boxStyle = new GUIStyle(GUI.skin.box);
            var bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.8f));
            bgTex.Apply();
            boxStyle.normal.background = bgTex;

            stylesInitialised = true;
        }
    }
}
