// CompanionAppMain.cs — Main controller for the PC-side companion application.
// Provides a full-screen status panel with input visualization and IP configuration.
// Desktop-only: excluded from UWP (HoloLens 2) builds.
#if !UNITY_WSA

using UnityEngine;
using AutoSim.Networking;

namespace AutoSim.CompanionApp
{
    /// <summary>
    /// Place this on a single GameObject in a dedicated companion-app scene.
    /// It manages the <see cref="UDPInputSender"/>, displays G920 status, renders
    /// real-time input bars, and lets the user configure the HoloLens 2 target IP.
    /// </summary>
    [AddComponentMenu("AutoSim/CompanionApp/Companion App Main")]
    public class CompanionAppMain : MonoBehaviour
    {
        // ───────────────────────── Inspector ─────────────────────────
        [Header("Network Target")]
        [Tooltip("IP address of the HoloLens 2 to send input data to.")]
        [SerializeField] private string targetIP = "192.168.1.100";

        [Tooltip("Target UDP port (must match UDPInputReceiver on HoloLens).")]
        [SerializeField] private int targetPort = AutoSimConstants.DEFAULT_UDP_PORT;

        [Header("Steering Wheel")]
        [Tooltip("Desired wheel rotation range in degrees.")]
        [SerializeField] private int steeringWheelRange = 900;

        // ───────────────────────── Private ───────────────────────────
        private UDPInputSender _sender;
        private bool _isSending;

        // GUI helpers
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _instructionStyle;
        private GUIStyle _buttonStyle;
        private Texture2D _barTex;
        private Texture2D _bgTex;
        private bool _stylesInitialized;

        // ───────────────────────── Unity lifecycle ───────────────────
        private void Start()
        {
            // Find or create the sender component on this GameObject.
            _sender = GetComponent<UDPInputSender>();
            if (_sender == null)
                _sender = gameObject.AddComponent<UDPInputSender>();

            // Apply inspector settings to the sender.
            _sender.TargetIP = targetIP;
            _sender.TargetPort = targetPort;
            _sender.UpdateRemoteEndPoint();

            // Start paused — the user clicks "Start Sending" when ready.
            _sender.IsSending = false;
            _isSending = false;

            Debug.Log("[CompanionAppMain] Ready. Configure target IP and press Start Sending.");
        }

        private void OnDestroy()
        {
            if (_barTex != null) Destroy(_barTex);
            if (_bgTex != null) Destroy(_bgTex);
        }

        // ───────────────────────── GUI ───────────────────────────────
        private void OnGUI()
        {
            EnsureStyles();

            float panelW = Mathf.Min(520f, Screen.width - 40f);
            float panelH = Mathf.Min(620f, Screen.height - 40f);
            float panelX = (Screen.width - panelW) * 0.5f;
            float panelY = (Screen.height - panelH) * 0.5f;

            // Panel background.
            GUI.Box(new Rect(panelX, panelY, panelW, panelH), GUIContent.none);

            GUILayout.BeginArea(new Rect(panelX + 16f, panelY + 12f,
                                          panelW - 32f, panelH - 24f));

            // ── Title ──
            GUILayout.Label("AutoSim — Companion App", _headerStyle);
            GUILayout.Space(8);

            // ── G920 Status ──
            DrawSection("Logitech G920");
            GUILayout.Label($"SDK Initialized: {(_sender != null && _sender.IsSDKInitialized)}", _labelStyle);
            GUILayout.Label($"Wheel Connected: {(_sender != null && _sender.IsWheelConnected)}", _labelStyle);
            GUILayout.Space(6);

            // ── Network Target ──
            DrawSection("Network Target");
            GUILayout.BeginHorizontal();
            GUILayout.Label("HoloLens IP:", _labelStyle, GUILayout.Width(90f));
            targetIP = GUILayout.TextField(targetIP, 45, GUILayout.Width(180f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Port:", _labelStyle, GUILayout.Width(90f));
            string portStr = GUILayout.TextField(targetPort.ToString(), 6, GUILayout.Width(80f));
            if (int.TryParse(portStr, out int parsedPort))
                targetPort = parsedPort;
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // ── Start / Stop button ──
            string btnLabel = _isSending ? "■  Stop Sending" : "►  Start Sending";
            if (GUILayout.Button(btnLabel, _buttonStyle, GUILayout.Height(36f)))
            {
                _isSending = !_isSending;
                if (_sender != null)
                {
                    _sender.TargetIP = targetIP;
                    _sender.TargetPort = targetPort;
                    _sender.UpdateRemoteEndPoint();
                    _sender.IsSending = _isSending;
                }
            }

            GUILayout.Space(8);

            // ── Transmission Stats ──
            DrawSection("Transmission");
            GUILayout.Label($"Sending: {_isSending}", _labelStyle);
            GUILayout.Label($"Packets Sent: {(_sender != null ? _sender.PacketsSent : 0)}", _labelStyle);
            GUILayout.Space(6);

            // ── Input Visualization ──
            if (_sender != null && _sender.IsWheelConnected)
            {
                DrawSection("Input Values");

                InputDataPacket pkt = _sender.LastSentPacket;

                // Steering bar: centered at middle, extends left/right.
                DrawSteeringBar("Steering", pkt.steering);
                GUILayout.Space(2);
                DrawBar("Throttle", pkt.throttle, new Color(0.2f, 0.8f, 0.2f));
                GUILayout.Space(2);
                DrawBar("Brake", pkt.brake, new Color(0.9f, 0.2f, 0.2f));
                GUILayout.Space(2);
                DrawBar("Clutch", pkt.clutch, new Color(0.3f, 0.5f, 0.9f));
                GUILayout.Space(2);
                GUILayout.Label($"Buttons: 0x{pkt.buttons:X4}", _valueStyle);
            }

            GUILayout.FlexibleSpace();

            // ── Instructions ──
            GUILayout.Label(
                "1. Connect the Logitech G920 via USB.\n" +
                "2. Enter the HoloLens 2 IP address above.\n" +
                "3. Press Start Sending.\n" +
                "4. Launch the AutoSim scene on HoloLens 2.",
                _instructionStyle);

            GUILayout.EndArea();
        }

        // ───────────────────────── Drawing helpers ───────────────────

        /// <summary>Draws a section header label.</summary>
        private void DrawSection(string title)
        {
            GUILayout.Space(4);
            GUILayout.Label($"── {title} ──", _valueStyle);
            GUILayout.Space(2);
        }

        /// <summary>Draws a horizontal bar for a 0…1 value (throttle, brake, clutch).</summary>
        private void DrawBar(string label, float value01, Color barColor)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: {value01:F2}", _labelStyle, GUILayout.Width(130f));

            Rect barRect = GUILayoutUtility.GetRect(200f, 16f);

            // Background.
            GUI.DrawTexture(barRect, _bgTex);

            // Filled portion.
            Color prev = GUI.color;
            GUI.color = barColor;
            GUI.DrawTexture(
                new Rect(barRect.x, barRect.y,
                         barRect.width * Mathf.Clamp01(value01), barRect.height),
                _barTex);
            GUI.color = prev;

            GUILayout.EndHorizontal();
        }

        /// <summary>Draws a centred steering bar (-1…+1).</summary>
        private void DrawSteeringBar(string label, float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: {value:F2}", _labelStyle, GUILayout.Width(130f));

            Rect barRect = GUILayoutUtility.GetRect(200f, 16f);
            GUI.DrawTexture(barRect, _bgTex);

            float center = barRect.x + barRect.width * 0.5f;
            float halfW = (value * 0.5f) * barRect.width;

            Color prev = GUI.color;
            GUI.color = new Color(0.9f, 0.7f, 0.1f);

            if (halfW >= 0)
                GUI.DrawTexture(new Rect(center, barRect.y, halfW, barRect.height), _barTex);
            else
                GUI.DrawTexture(new Rect(center + halfW, barRect.y, -halfW, barRect.height), _barTex);

            GUI.color = prev;

            // Center tick.
            GUI.DrawTexture(new Rect(center - 1f, barRect.y, 2f, barRect.height), _barTex);

            GUILayout.EndHorizontal();
        }

        /// <summary>Lazily creates GUI styles and textures.</summary>
        private void EnsureStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _headerStyle.normal.textColor = Color.white;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft
            };
            _labelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);

            _valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _valueStyle.normal.textColor = new Color(0.7f, 0.85f, 1f);

            _instructionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            _instructionStyle.normal.textColor = new Color(0.65f, 0.65f, 0.65f);

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };

            _barTex = MakeSolidTexture(Color.white);
            _bgTex = MakeSolidTexture(new Color(0.2f, 0.2f, 0.2f, 0.8f));
        }

        private static Texture2D MakeSolidTexture(Color c)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, c);
            tex.Apply();
            return tex;
        }
    }
}

#endif // !UNITY_WSA
