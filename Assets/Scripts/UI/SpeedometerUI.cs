// SpeedometerUI.cs — Automotive simulator digital instrument cluster HUD with Transmission & Telemetry.
// Features digital speedometer, rev counter with shift lights, Automatic (R-N-D) & Manual (1-6, N, R) gear displays,
// live pedal telemetry (gas/brake/clutch with 80% clutch interlock mark), steering angle, and PRESS CLUTCH warnings.
using UnityEngine;
using AutoSim.Vehicle;
using AutoSim.Input;

namespace AutoSim.UI
{
    /// <summary>
    /// Renders a modern automotive digital instrument cluster HUD.
    /// Features digital speed, RPM arc bar with shift lights, transmission selector (Auto R-N-D / Manual 1-6),
    /// live pedal telemetry bars, steering wheel angle gauge, and dynamic clutch shift alerts.
    /// Toggle visibility with [F1].
    /// </summary>
    [AddComponentMenu("AutoSim/UI/Speedometer UI")]
    public class SpeedometerUI : MonoBehaviour
    {
        // ──────────────────────────── Serialized Settings ─────────────────────────────

        [Header("Data Sources (Auto-discovered if null)")]
        [SerializeField] private Speedometer speedometer;
        [SerializeField] private VehicleController vehicleController;
        [SerializeField] private TransmissionController transmission;

        [Header("Cluster Layout")]
        [Tooltip("Width of the central digital cluster HUD.")]
        [SerializeField] private float clusterWidth = 520f;

        [Tooltip("Height of the central digital cluster HUD.")]
        [SerializeField] private float clusterHeight = 155f;

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
        private GUIStyle warningAlertStyle;
        private GUIStyle modeBadgeStyle;

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
        private Texture2D alertBgTex;
        private Texture2D gearHighlightTex;

        private bool stylesInit = false;
        private float shiftFlashTimer = 0f;

        // ──────────────────────────── Unity Lifecycle ─────────────────────────────────

        private void Start()
        {
            if (speedometer == null)
                speedometer = FindObjectOfType<Speedometer>();

            if (vehicleController == null)
                vehicleController = FindObjectOfType<VehicleController>();

            if (transmission == null && vehicleController != null)
                transmission = vehicleController.Transmission;

            if (transmission == null)
                transmission = FindObjectOfType<TransmissionController>();
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
            DestroyTex(alertBgTex);
            DestroyTex(gearHighlightTex);
        }

        // ──────────────────────────── GUI Styling Setup ───────────────────────────────

        private void InitStyles()
        {
            if (stylesInit) return;

            // Semi-transparent deep carbon cockpit glass
            panelBgTex = MakeTex(2, 2, new Color(0.04f, 0.05f, 0.08f, 0.90f));
            borderTex = MakeTex(2, 2, new Color(0.18f, 0.65f, 0.95f, 0.75f));
            barBgTex = MakeTex(2, 2, new Color(0.12f, 0.13f, 0.16f, 0.95f));

            rpmGreenTex = MakeTex(2, 2, new Color(0.15f, 0.85f, 0.40f, 0.95f));
            rpmYellowTex = MakeTex(2, 2, new Color(1.0f, 0.82f, 0.15f, 0.95f));
            rpmRedTex = MakeTex(2, 2, new Color(1.0f, 0.20f, 0.20f, 0.95f));

            gasGreenTex = MakeTex(2, 2, new Color(0.10f, 0.95f, 0.45f, 0.95f));
            brakeRedTex = MakeTex(2, 2, new Color(1.0f, 0.22f, 0.22f, 0.95f));
            clutchOrangeTex = MakeTex(2, 2, new Color(1.0f, 0.60f, 0.10f, 0.95f));
            steerCyanTex = MakeTex(2, 2, new Color(0.20f, 0.75f, 1.0f, 0.95f));
            whiteMarkerTex = MakeTex(2, 2, Color.white);

            alertBgTex = MakeTex(2, 2, new Color(0.85f, 0.12f, 0.12f, 0.92f));
            gearHighlightTex = MakeTex(2, 2, new Color(0.25f, 0.45f, 0.70f, 0.60f));

            speedNumStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 46,
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
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            gearActiveStyle.normal.textColor = new Color(1.0f, 0.85f, 0.20f);

            gearInactiveStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            gearInactiveStyle.normal.textColor = new Color(0.40f, 0.45f, 0.50f);

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

            modeBadgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            warningAlertStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            warningAlertStyle.normal.textColor = Color.white;

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

            if (transmission == null && vehicleController != null)
                transmission = vehicleController.Transmission;

            if (speedometer != null)
            {
                speedKmh = speedometer.SpeedKmh;
                rpm = speedometer.Rpm;
            }
            else if (vehicleController != null)
            {
                speedKmh = vehicleController.SpeedKmh;
                rpm = vehicleController.Rpm;
            }

            if (transmission != null)
            {
                gear = transmission.CurrentGear;
            }
            else
            {
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

            // Warning Banner if shift was rejected (e.g. PRESS CLUTCH)
            if (transmission != null && transmission.IsShiftRejected && transmission.ShiftRejectTimer > 0f)
            {
                float bannerW = 340f;
                float bannerH = 34f;
                float bannerX = (Screen.width - bannerW) * 0.5f;
                float bannerY = y - bannerH - 8f;

                Rect bannerRect = new Rect(bannerX, bannerY, bannerW, bannerH);
                bool flashAlert = Mathf.Sin(shiftFlashTimer * 1.5f) > 0f;
                if (flashAlert)
                {
                    GUI.DrawTexture(bannerRect, alertBgTex);
                    GUI.Label(bannerRect, $"⚠  {transmission.ShiftRejectReason}  ⚠", warningAlertStyle);
                }
            }

            // Outer Glow & Border
            GUI.DrawTexture(new Rect(x - 2, y - 2, clusterWidth + 4, clusterHeight + 4), borderTex);
            // Main Glass Panel
            GUI.DrawTexture(clusterRect, panelBgTex);

            // ──────────────────────────── Header Badge ────────────────────────────────
            Rect badgeRect = new Rect(x + 15f, y + 4f, clusterWidth - 30f, 16f);
            GUI.Label(badgeRect, "◆  AUTOSIM SPORT GT CLUSTER  |  LOGITECH G920 FFB  ◆", titleBadgeStyle);

            // ──────────────────────────── RPM Rev Counter Bar ─────────────────────────
            float maxRpm = 8000f;
            float rpmNorm = Mathf.Clamp01(rpm / maxRpm);
            Rect rpmBarRect = new Rect(x + 18f, y + 23f, clusterWidth - 36f, 14f);
            DrawRpmBar(rpmBarRect, rpmNorm, rpm);

            // ──────────────────────────── Speed & Unit ────────────────────────────────
            Rect speedRect = new Rect(x + 20f, y + 42f, 115f, 52f);
            GUI.Label(speedRect, $"{speedKmh:F0}", speedNumStyle);

            Rect unitRect = new Rect(x + 140f, y + 54f, 65f, 22f);
            GUI.Label(unitRect, "KM/H", unitLabelStyle);

            Rect speedSub = new Rect(x + 140f, y + 72f, 65f, 18f);
            float mph = speedKmh * 0.621371f;
            GUI.Label(speedSub, $"{mph:F0} MPH", labelSmallStyle);

            // ──────────────────────────── Mode & Gear Section ─────────────────────────
            bool isAuto = transmission == null || transmission.Mode == TransmissionMode.Automatic;
            string modeText = isAuto ? "AUTOMAT [M]" : "MANUAL [M]";
            Color modeColor = isAuto ? new Color(0.3f, 0.85f, 1.0f) : new Color(1.0f, 0.65f, 0.2f);
            modeBadgeStyle.normal.textColor = modeColor;

            Rect modeRect = new Rect(x + 215f, y + 40f, 90f, 16f);
            GUI.Label(modeRect, modeText, modeBadgeStyle);

            // Gear Display Box
            Rect gearBoxRect = new Rect(x + 225f, y + 58f, 70f, 44f);
            GUI.DrawTexture(gearBoxRect, barBgTex);

            // Gear shift flash highlight
            if (transmission != null && transmission.GearChangeFlashTimer > 0f)
            {
                GUI.DrawTexture(gearBoxRect, gearHighlightTex);
            }

            string gearText;
            if (gear == -1) gearText = "R";
            else if (gear == 0) gearText = "N";
            else gearText = gear.ToString();

            GUI.Label(gearBoxRect, gearText, gearActiveStyle);

            // Selector subtitle: [R] [N] [D] for Auto, or physical gear hint for Manual
            Rect selectorRect = new Rect(x + 210f, y + 104f, 100f, 16f);
            if (isAuto && transmission != null)
            {
                string rSel = transmission.CurrentAutoSelector == AutoSelector.R ? "[R]" : " R ";
                string nSel = transmission.CurrentAutoSelector == AutoSelector.N ? "[N]" : " N ";
                string dSel = transmission.CurrentAutoSelector == AutoSelector.D ? "[D]" : " D ";
                GUI.Label(selectorRect, $"{rSel}  {nSel}  {dSel}", gearInactiveStyle);
            }
            else
            {
                GUI.Label(selectorRect, "1-6 H-SHIFTER", gearInactiveStyle);
            }

            // ──────────────────────────── Live Telemetry Pedals ───────────────────────
            float pedalStartX = x + 325f;
            float pedalY = y + 44f;
            float pedalBarW = 110f;
            float pedalBarH = 10f;

            // Throttle (Gas)
            GUI.Label(new Rect(pedalStartX, pedalY, 50f, 14f), "GAS", labelSmallStyle);
            DrawMetricBar(new Rect(pedalStartX + 44f, pedalY + 2f, pedalBarW, pedalBarH), throttle, gasGreenTex);
            GUI.Label(new Rect(pedalStartX + 158f, pedalY, 35f, 14f), $"{throttle * 100f:F0}%", labelSmallStyle);

            // Brake
            GUI.Label(new Rect(pedalStartX, pedalY + 18f, 50f, 14f), "BRK", labelSmallStyle);
            DrawMetricBar(new Rect(pedalStartX + 44f, pedalY + 20f, pedalBarW, pedalBarH), brake, brakeRedTex);
            GUI.Label(new Rect(pedalStartX + 158f, pedalY + 18f, 35f, 14f), $"{brake * 100f:F0}%", labelSmallStyle);

            // Clutch (with 80% threshold line marker)
            GUI.Label(new Rect(pedalStartX, pedalY + 36f, 50f, 14f), "CLT", labelSmallStyle);
            DrawClutchBar(new Rect(pedalStartX + 44f, pedalY + 38f, pedalBarW, pedalBarH), clutch);
            GUI.Label(new Rect(pedalStartX + 158f, pedalY + 36f, 35f, 14f), $"{clutch * 100f:F0}%", labelSmallStyle);

            // ──────────────────────────── Steering Indicator Bar ──────────────────────
            float steerY = y + 122f;
            Rect steerBarRect = new Rect(x + 140f, steerY + 2f, 240f, 10f);
            GUI.Label(new Rect(x + 65f, steerY - 1f, 75f, 16f), "STEERING", labelSmallStyle);
            DrawSteeringBar(steerBarRect, steering);

            float degrees = steering * 450f;
            string steerDeg = degrees >= 0 ? $"+{degrees:F0}°" : $"{degrees:F0}°";
            GUI.Label(new Rect(x + 390f, steerY - 1f, 65f, 16f), steerDeg, labelSmallStyle);

            // Footer note
            GUI.Label(new Rect(x + 18f, y + 138f, clusterWidth - 36f, 14f), "[M] Schimbă Automat/Manual  |  [Padele] R-N-D  |  [F1] HUD  |  [C] Cameră", titleBadgeStyle);
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

        private void DrawClutchBar(Rect r, float clutchFraction)
        {
            GUI.DrawTexture(r, barBgTex);
            float w = r.width * Mathf.Clamp01(clutchFraction);
            if (w > 0f)
            {
                GUI.DrawTexture(new Rect(r.x, r.y, w, r.height), clutchOrangeTex);
            }

            // 80% threshold marker line (clutch engagement point for manual shifting)
            float thresholdX = r.x + r.width * 0.80f;
            GUI.DrawTexture(new Rect(thresholdX - 1f, r.y - 2f, 2f, r.height + 4f), whiteMarkerTex);
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
