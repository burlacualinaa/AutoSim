// SpeedometerUI.cs — On-screen speedometer HUD using OnGUI() (no Canvas dependency).
// Displays speed, gear, RPM bar, and steering indicator in the bottom-right corner.
using UnityEngine;
using AutoSim.Vehicle;

namespace AutoSim.UI
{
    /// <summary>
    /// Immediate-mode GUI speedometer display. Uses <see cref="Speedometer"/> (or
    /// <see cref="VehicleController"/> directly) for data. Toggle visibility with F1.
    /// </summary>
    [AddComponentMenu("AutoSim/UI/Speedometer UI")]
    public class SpeedometerUI : MonoBehaviour
    {
        // ---------------------------------------------------------------
        //  Serialized References
        // ---------------------------------------------------------------

        [Header("Data Source (assign one)")]
        [Tooltip("Preferred data source. If null, VehicleController is used directly.")]
        [SerializeField] private Speedometer speedometer;

        [Tooltip("Fallback data source if Speedometer is not assigned.")]
        [SerializeField] private VehicleController vehicleController;

        [Header("Display Settings")]
        [Tooltip("Panel width in pixels.")]
        [SerializeField] private float panelWidth = 220f;

        [Tooltip("Panel height in pixels.")]
        [SerializeField] private float panelHeight = 180f;

        [Tooltip("Margin from screen edges in pixels.")]
        [SerializeField] private float margin = 15f;

        // ---------------------------------------------------------------
        //  Internal State
        // ---------------------------------------------------------------

        private bool isVisible = true;

        // Cached GUIStyle objects (created once in OnGUI)
        private GUIStyle speedStyle;
        private GUIStyle labelStyle;
        private GUIStyle boxStyle;
        private bool stylesInitialised;

        // ---------------------------------------------------------------
        //  Unity Lifecycle
        // ---------------------------------------------------------------

        private void Start()
        {
            // Auto-discover data sources if not assigned
            if (speedometer == null)
                speedometer = FindObjectOfType<Speedometer>();

            if (vehicleController == null)
                vehicleController = FindObjectOfType<VehicleController>();
        }

        private void Update()
        {
            // Toggle visibility with F1
            if (UnityEngine.Input.GetKeyDown(KeyCode.F1))
                isVisible = !isVisible;
        }

        // ---------------------------------------------------------------
        //  OnGUI Rendering
        // ---------------------------------------------------------------

        private void OnGUI()
        {
            if (!isVisible)
                return;

            EnsureStyles();

            // Read data
            float speedKmh = 0f;
            float rpm = 0f;
            float steering = 0f;
            int gear = 1;
            float normalizedSpeed = 0f;

            if (speedometer != null)
            {
                speedKmh = speedometer.SpeedKmh;
                rpm = speedometer.Rpm;
                gear = speedometer.GearEstimate;
                normalizedSpeed = speedometer.NormalizedSpeed;
                steering = vehicleController != null ? vehicleController.CurrentSteeringAngle / AutoSimConstants.MAX_STEERING_ANGLE : 0f;
            }
            else if (vehicleController != null)
            {
                speedKmh = vehicleController.SpeedKmh;
                rpm = vehicleController.Rpm;
                gear = EstimateGear(speedKmh);
                normalizedSpeed = Mathf.Clamp01(speedKmh / AutoSimConstants.MAX_SPEED_KMH);
                steering = vehicleController.CurrentSteeringAngle / AutoSimConstants.MAX_STEERING_ANGLE;
            }

            // Panel position: bottom-right
            float x = Screen.width - panelWidth - margin;
            float y = Screen.height - panelHeight - margin;
            Rect panelRect = new Rect(x, y, panelWidth, panelHeight);

            // Background box
            GUI.Box(panelRect, "", boxStyle);

            GUILayout.BeginArea(new Rect(x + 10f, y + 8f, panelWidth - 20f, panelHeight - 16f));

            // --- Speed (large, colour-coded) ---
            Color speedColour = GetSpeedColour(speedKmh);
            speedStyle.normal.textColor = speedColour;
            GUILayout.Label($"{speedKmh:F0}", speedStyle);
            GUILayout.Label("km/h", labelStyle);

            GUILayout.Space(4f);

            // --- Gear ---
            labelStyle.normal.textColor = Color.white;
            GUILayout.Label($"Gear: {gear}", labelStyle);

            GUILayout.Space(4f);

            // --- RPM Bar ---
            float rpmNorm = Mathf.Clamp01(rpm / 8000f);
            DrawHorizontalBar("RPM", rpmNorm, GetRpmColour(rpmNorm));

            GUILayout.Space(4f);

            // --- Steering Indicator ---
            DrawSteeringIndicator(steering);

            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------
        //  Drawing Helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Draws a labelled horizontal progress bar.
        /// </summary>
        private void DrawHorizontalBar(string label, float normalizedValue, Color barColour)
        {
            GUILayout.Label(label, labelStyle);

            Rect barRect = GUILayoutUtility.GetRect(panelWidth - 20f, 12f);
            GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            GUI.DrawTexture(barRect, Texture2D.whiteTexture);

            Rect fillRect = new Rect(barRect.x, barRect.y,
                                     barRect.width * normalizedValue, barRect.height);
            GUI.color = barColour;
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

            GUI.color = Color.white; // Reset
        }

        /// <summary>
        /// Draws a simple needle-style steering indicator (-1 left to +1 right).
        /// </summary>
        private void DrawSteeringIndicator(float normSteering)
        {
            GUILayout.Label("Steering", labelStyle);

            Rect barRect = GUILayoutUtility.GetRect(panelWidth - 20f, 14f);

            // Background track
            GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            GUI.DrawTexture(barRect, Texture2D.whiteTexture);

            // Centre mark
            float centreX = barRect.x + barRect.width * 0.5f;
            GUI.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            GUI.DrawTexture(new Rect(centreX - 1f, barRect.y, 2f, barRect.height), Texture2D.whiteTexture);

            // Needle position: normSteering is -1..+1, map to bar width
            float needleX = centreX + (normSteering * barRect.width * 0.5f);
            float needleWidth = 6f;
            GUI.color = Color.cyan;
            GUI.DrawTexture(new Rect(needleX - needleWidth * 0.5f, barRect.y, needleWidth, barRect.height),
                            Texture2D.whiteTexture);

            GUI.color = Color.white; // Reset
        }

        // ---------------------------------------------------------------
        //  Colour Helpers
        // ---------------------------------------------------------------

        /// <summary>Speed colour: green &lt; 60, yellow &lt; 100, red ≥ 100 km/h.</summary>
        private static Color GetSpeedColour(float kmh)
        {
            if (kmh < 60f) return Color.green;
            if (kmh < 100f) return Color.yellow;
            return Color.red;
        }

        /// <summary>RPM colour: green → yellow → red as RPM increases.</summary>
        private static Color GetRpmColour(float normalizedRpm)
        {
            if (normalizedRpm < 0.6f) return Color.green;
            if (normalizedRpm < 0.85f) return Color.yellow;
            return Color.red;
        }

        // ---------------------------------------------------------------
        //  Gear Estimation Fallback
        // ---------------------------------------------------------------

        /// <summary>Fallback gear estimation when no Speedometer component is available.</summary>
        private static int EstimateGear(float speedKmh)
        {
            if (speedKmh < 20f) return 1;
            if (speedKmh < 40f) return 2;
            if (speedKmh < 70f) return 3;
            if (speedKmh < 100f) return 4;
            if (speedKmh < 140f) return 5;
            return 6;
        }

        // ---------------------------------------------------------------
        //  Style Initialisation
        // ---------------------------------------------------------------

        private void EnsureStyles()
        {
            if (stylesInitialised)
                return;

            speedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };
            labelStyle.normal.textColor = Color.white;

            boxStyle = new GUIStyle(GUI.skin.box);
            var bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.75f));
            bgTex.Apply();
            boxStyle.normal.background = bgTex;

            stylesInitialised = true;
        }
    }
}
