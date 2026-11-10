// SpeedometerUI.cs — High-performance automotive simulator digital instrument cluster HUD.
// Features digital speedometer, dynamic LED rev counter with shift lights, sport gear indicator,
// live pedal telemetry (gas/brake/clutch), steering angle readout, and modern GT carbon styling.
using UnityEngine;
using AutoSim.Vehicle;
using AutoSim.Input;

namespace AutoSim.UI
{
    /// <summary>
    /// Renders a modern automotive digital instrument cluster HUD reminiscent of
    /// high-end GT racing simulators (Assetto Corsa / Gran Turismo / Porsche Digital Cockpit).
    /// Features digital speed, RPM arc bar with shift lights, gear selector,
    /// live pedal telemetry bars, and steering wheel angle gauge.
    /// Toggle visibility with [F1].
    /// </summary>
    [AddComponentMenu("AutoSim/UI/Speedometer UI")]
    public class SpeedometerUI : MonoBehaviour
    {
        // ──────────────────────────── Serialized Settings ─────────────────────────────

        [Header("Data Sources (Auto-discovered if null)")]
        [SerializeField] private Speedometer speedometer;
        [SerializeField] private VehicleController vehicleController;

        [Header("Cluster Layout")]
        [Tooltip("Width of the central digital cluster HUD.")]
        [SerializeField] private float clusterWidth = 460f;

        [Tooltip("Height of the central digital cluster HUD.")]
        [SerializeField] private float clusterHeight = 145f;

        [Tooltip("Bottom margin from screen edge in pixels.")]
        [SerializeField] private float bottomMargin = 20f;

        [Tooltip("Toggle key to show or hide instrument cluster.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;

        [Tooltip("Start visible on scene load.")]
        [SerializeField] private bool isVisible = true;

        // ──────────────────────────── Internal State & Textures ───────────────────────

        private GUIStyle speedNumStyle;
        private GUIStyle unitLabelStyle;
        private GUIStyle gearActiveStyle;
        private GUIStyle gearInactiveStyle;
        private GUIStyle labelSmallStyle;
        private GUIStyle titleBadgeStyle;

        private Texture2D panelBgTex;
        private Texture2D borderTex;
        private Texture2D barBgTex;
        private Texture2D rpmGreenTex;
        private Texture2D rpmYellowTex;
        private Texture2D rpmRedTex;
        private Texture2D gasGreenTex;
        private Texture2D brakeRedTex;
        private Texture2D clutchOrangeTex;
        private Texture2D steerCyanTex;
        private Texture2D whiteMarkerTex;

        private bool stylesInit = false;
        private float shiftFlashTimer = 0f;

        // ──────────────────────────── Unity Lifecycle ─────────────────────────────────

        private void Start()
        {
            if (speedometer == null)
                speedometer = FindObjectOfType<Speedometer>();

            if (vehicleController == null)
                vehicleController = FindObjectOfType<VehicleController>();
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(toggleKey))
                isVisible = !isVisible;

            shiftFlashTimer += Time.unscaledDeltaTime * 12f;
        }

        private void OnDestroy()
        {
            DestroyTex(panelBgTex);
            DestroyTex(borderTex);
            DestroyTex(barBgTex);
            DestroyTex(rpmGreenTex);
            DestroyTex(rpmYellowTex);
            DestroyTex(rpmRedTex);
            DestroyTex(gasGreenTex);
            DestroyTex(brakeRedTex);
            DestroyTex(clutchOrangeTex);
            DestroyTex(steerCyanTex);
            DestroyTex(whiteMarkerTex);
        }

        // ──────────────────────────── GUI Styling Setup ───────────────────────────────

        private void InitStyles()
        {
            if (stylesInit) return;

            // Semi-transparent deep carbon cockpit glass
            panelBgTex = MakeTex(2, 2, new Color(0.04f, 0.05f, 0.08f, 0.88f));
            borderTex = MakeTex(2, 2, new Color(0.18f, 0.65f, 0.95f, 0.70f));
            barBgTex = MakeTex(2, 2, new Color(0.12f, 0.13f, 0.16f, 0.95f));

            rpmGreenTex = MakeTex(2, 2, new Color(0.15f, 0.85f, 0.40f, 0.95f));
            rpmYellowTex = MakeTex(2, 2, new Color(1.0f, 0.82f, 0.15f, 0.95f));
            rpmRedTex = MakeTex(2, 2, new Color(1.0f, 0.20f, 0.20f, 0.95f));

            gasGreenTex = MakeTex(2, 2, new Color(0.10f, 0.95f, 0.45f, 0.95f));
            brakeRedTex = MakeTex(2, 2, new Color(1.0f, 0.22f, 0.22f, 0.95f));
            clutchOrangeTex = MakeTex(2, 2, new Color(1.0f, 0.60f, 0.10f, 0.95f));
            steerCyanTex = MakeTex(2, 2, new Color(0.20f, 0.75f, 1.0f, 0.95f));
            whiteMarkerTex = MakeTex(2, 2, Color.white);

            speedNumStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            speedNumStyle.normal.textColor = Color.white;

            unitLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            unitLabelStyle.normal.textColor = new Color(0.6f, 0.8f, 1.0f);

            gearActiveStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            gearActiveStyle.normal.textColor = new Color(1.0f, 0.85f, 0.20f);

            gearInactiveStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            gearInactiveStyle.normal.textColor = new Color(0.35f, 0.40f, 0.45f);

            labelSmallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };
            labelSmallStyle.normal.textColor = new Color(0.70f, 0.75f, 0.80f);

            titleBadgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleBadgeStyle.normal.textColor = new Color(0.2f, 0.75f, 1.0f);

            stylesInit = true;
        }

        // ──────────────────────────── OnGUI Rendering ─────────────────────────────────

        private void OnGUI()
        {
            if (!isVisible) return;

            InitStyles();

            // 1. Fetch current vehicle & input telemetry
            float speedKmh = 0f;
            float rpm = 0f;
            int gear = 1;
            float throttle = 0f;
            float brake = 0f;
            float clutch = 0f;
            float steering = 0f;

            if (speedometer != null)
            {
                speedKmh = speedometer.SpeedKmh;
                rpm = speedometer.Rpm;
                gear = speedometer.GearEstimate;
            }
            else if (vehicleController != null)
            {
                speedKmh = vehicleController.SpeedKmh;
                rpm = vehicleController.Rpm;
                gear = Mathf.Clamp(Mathf.FloorToInt(speedKmh / 25f) + 1, 1, 6);
            }

            if (vehicleController != null && vehicleController.ActiveInputProvider != null)
            {
                var input = vehicleController.ActiveInputProvider;
                throttle = input.Throttle;
                brake = input.Brake;
                clutch = input.Clutch;
                steering = input.Steering;
            }

            // 2. Position Cluster at bottom-center of screen
            float x = (Screen.width - clusterWidth) * 0.5f;
            float y = Screen.height - clusterHeight - bottomMargin;
            Rect clusterRect = new Rect(x, y, clusterWidth, clusterHeight);

            // Outer Glow & Border
            GUI.DrawTexture(new Rect(x - 2, y - 2, clusterWidth + 4, clusterHeight + 4), borderTex);
            // Main Glass Panel
            GUI.DrawTexture(clusterRect, panelBgTex);

            // ──────────────────────────── Header Badge ────────────────────────────────
            Rect badgeRect = new Rect(x + 15f, y + 4f, clusterWidth - 30f, 16f);
            GUI.Label(badgeRect, "◆  AUTOSIM SPORT GT CLUSTER  |  OPENXR MR SIMULATOR  ◆", titleBadgeStyle);

            // ──────────────────────────── RPM Rev Counter Bar ─────────────────────────
            float maxRpm = 8000f;
            float rpmNorm = Mathf.Clamp01(rpm / maxRpm);
            Rect rpmBarRect = new Rect(x + 18f, y + 24f, clusterWidth - 36f, 14f);
            DrawRpmBar(rpmBarRect, rpmNorm, rpm);

            // ──────────────────────────── Speed & Unit ────────────────────────────────
            Rect speedRect = new Rect(x + 22f, y + 42f, 110f, 52f);
            GUI.Label(speedRect, $"{speedKmh:F0}", speedNumStyle);

            Rect unitRect = new Rect(x + 135f, y + 54f, 65f, 22f);
            GUI.Label(unitRect, "KM/H", unitLabelStyle);

            Rect speedSub = new Rect(x + 135f, y + 72f, 65f, 18f);
            float mph = speedKmh * 0.621371f;
            GUI.Label(speedSub, $"{mph:F0} MPH", labelSmallStyle);

            // ──────────────────────────── Gear Indicator ──────────────────────────────
            Rect gearBoxRect = new Rect(x + 205f, y + 44f, 60f, 54f);
            GUI.DrawTexture(gearBoxRect, barBgTex);

            string gearText = (speedKmh < 0.2f && throttle < 0.05f) ? "N" : gear.ToString();
            GUI.Label(gearBoxRect, gearText, gearActiveStyle);

            Rect gearLabel = new Rect(x + 205f, y + 98f, 60f, 14f);
            GUI.Label(gearLabel, "GEAR", labelSmallStyle);

            // ──────────────────────────── Live Telemetry Pedals ───────────────────────
            float pedalStartX = x + 285f;
            float pedalY = y + 44f;
            float pedalBarW = 95f;
            float pedalBarH = 10f;

            // Throttle (Gas)
            GUI.Label(new Rect(pedalStartX, pedalY, 50f, 14f), "GAS", labelSmallStyle);
            DrawMetricBar(new Rect(pedalStartX + 42f, pedalY + 2f, pedalBarW, pedalBarH), throttle, gasGreenTex);
            GUI.Label(new Rect(pedalStartX + 142f, pedalY, 35f, 14f), $"{throttle * 100f:F0}%", labelSmallStyle);

            // Brake
            GUI.Label(new Rect(pedalStartX, pedalY + 18f, 50f, 14f), "BRK", labelSmallStyle);
            DrawMetricBar(new Rect(pedalStartX + 42f, pedalY + 20f, pedalBarW, pedalBarH), brake, brakeRedTex);
            GUI.Label(new Rect(pedalStartX + 142f, pedalY + 18f, 35f, 14f), $"{brake * 100f:F0}%", labelSmallStyle);

            // Clutch
            GUI.Label(new Rect(pedalStartX, pedalY + 36f, 50f, 14f), "CLT", labelSmallStyle);
            DrawMetricBar(new Rect(pedalStartX + 42f, pedalY + 38f, pedalBarW, pedalBarH), clutch, clutchOrangeTex);
            GUI.Label(new Rect(pedalStartX + 142f, pedalY + 36f, 35f, 14f), $"{clutch * 100f:F0}%", labelSmallStyle);

            // ──────────────────────────── Steering Indicator Bar ──────────────────────
            float steerY = y + 115f;
            Rect steerBarRect = new Rect(x + 120f, steerY + 2f, 220f, 10f);
            GUI.Label(new Rect(x + 45f, steerY - 1f, 75f, 16f), "STEERING", labelSmallStyle);
            DrawSteeringBar(steerBarRect, steering);

            float degrees = steering * 450f;
            string steerDeg = degrees >= 0 ? $"+{degrees:F0}°" : $"{degrees:F0}°";
            GUI.Label(new Rect(x + 350f, steerY - 1f, 65f, 16f), steerDeg, labelSmallStyle);

            // Footer note
            GUI.Label(new Rect(x + 18f, y + 128f, clusterWidth - 36f, 14f), "[F1] Toggle Dashboard  |  [C] Cockpit/Chase View  |  [F3] Hardware Config", titleBadgeStyle);
        }

        // ──────────────────────────── Drawing Utilities ───────────────────────────────

        private void DrawRpmBar(Rect r, float fraction, float currentRpm)
        {
            GUI.DrawTexture(r, barBgTex);

            bool isRedline = currentRpm > 6500f;
            bool flashOn = Mathf.Sin(shiftFlashTimer) > 0f;

            if (isRedline && flashOn)
            {
                // Full shift light flash
                GUI.DrawTexture(r, rpmRedTex);
                return;
            }

            float fillW = r.width * Mathf.Clamp01(fraction);
            if (fillW > 0f)
            {
                Texture2D fillTex = (fraction < 0.65f) ? rpmGreenTex : (fraction < 0.85f ? rpmYellowTex : rpmRedTex);
                GUI.DrawTexture(new Rect(r.x, r.y, fillW, r.height), fillTex);
            }

            // White 6500 RPM Redline Marker tick
            float redlineX = r.x + r.width * (6500f / 8000f);
            GUI.DrawTexture(new Rect(redlineX, r.y - 2f, 2f, r.height + 4f), whiteMarkerTex);
        }

        private void DrawMetricBar(Rect r, float fraction, Texture2D fillTex)
        {
            GUI.DrawTexture(r, barBgTex);
            float w = r.width * Mathf.Clamp01(fraction);
            if (w > 0f)
            {
                GUI.DrawTexture(new Rect(r.x, r.y, w, r.height), fillTex);
            }
        }

        private void DrawSteeringBar(Rect r, float steerValue)
        {
            GUI.DrawTexture(r, barBgTex);

            float center = r.x + r.width * 0.5f;
            // Center notch
            GUI.DrawTexture(new Rect(center - 1f, r.y - 2f, 2f, r.height + 4f), whiteMarkerTex);

            float halfW = r.width * 0.5f;
            float barW = Mathf.Abs(steerValue) * halfW;

            if (steerValue >= 0f)
            {
                GUI.DrawTexture(new Rect(center, r.y, barW, r.height), steerCyanTex);
            }
            else
            {
                GUI.DrawTexture(new Rect(center - barW, r.y, barW, r.height), steerCyanTex);
            }
        }

        private static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D tex = new Texture2D(width, height);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private static void DestroyTex(Texture2D t)
        {
            if (t != null) Destroy(t);
        }
    }
}
