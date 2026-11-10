// NetworkManager.cs — Monitors the UDP connection state and displays a HUD indicator.
// Attach alongside UDPInputReceiver or reference one via the Inspector.
using System;
using UnityEngine;

namespace AutoSim.Networking
{
    /// <summary>
    /// Tracks the connection state to the PC companion app and provides
    /// events + a small on-screen indicator for debugging.
    /// </summary>
    [AddComponentMenu("AutoSim/Networking/Network Manager")]
    public class NetworkManager : MonoBehaviour
    {
        // ───────────────────────── Types ─────────────────────────────
        /// <summary>High-level connection state.</summary>
        public enum ConnectionState
        {
            /// <summary>No packets have ever been received, or the stream timed out.</summary>
            Disconnected,
            /// <summary>Packets are arriving within the timeout window.</summary>
            Receiving,
            /// <summary>Packets were arriving but have stopped for longer than the timeout.</summary>
            TimedOut
        }

        // ───────────────────────── Inspector ─────────────────────────
        [Header("References")]
        [Tooltip("The UDP receiver to monitor. Auto-found on this GameObject if left null.")]
        [SerializeField] private UDPInputReceiver udpReceiver;

        [Header("Connection Settings")]
        [Tooltip("Seconds without a packet before the connection is considered timed out.")]
        [SerializeField] private float connectionTimeout = AutoSimConstants.CONNECTION_TIMEOUT_SECONDS;

        [Header("UI")]
        [Tooltip("Show the small connection indicator in the top-right corner.")]
        [SerializeField] private bool showConnectionUI = true;

        // ───────────────────────── Public API ────────────────────────
        /// <summary>Current connection state.</summary>
        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        /// <summary>Human-readable status string.</summary>
        public string StatusText { get; private set; } = "Waiting...";

        /// <summary>Fired the first time a packet is received after being disconnected.</summary>
        public event Action OnConnected;

        /// <summary>Fired when the connection times out after previously receiving packets.</summary>
        public event Action OnDisconnected;

        // ───────────────────────── Private ───────────────────────────
        private ConnectionState _previousState = ConnectionState.Disconnected;
        private bool _everReceivedPacket;

        // GUI styling (lazy-initialized once).
        private GUIStyle _labelStyle;
        private Texture2D _greenTex;
        private Texture2D _redTex;
        private Texture2D _yellowTex;

        // ───────────────────────── Unity lifecycle ───────────────────
        private void Start()
        {
            if (udpReceiver == null)
                udpReceiver = GetComponent<UDPInputReceiver>();

            if (udpReceiver == null)
            {
                Debug.LogError("[NetworkManager] No UDPInputReceiver found. " +
                               "Attach one to this GameObject or assign via Inspector.");
                enabled = false;
                return;
            }
        }

        private void Update()
        {
            EvaluateState();
        }

        private void OnGUI()
        {
            if (!showConnectionUI) return;

#if UNITY_EDITOR
            // On PC Editor, only show if receiving packets from an external source to avoid overlap with Logitech HUD
            if (State != ConnectionState.Receiving) return;
#endif

            DrawConnectionIndicator();
        }

        private void OnDestroy()
        {
            // Clean up textures we allocated.
            if (_greenTex != null) Destroy(_greenTex);
            if (_redTex != null) Destroy(_redTex);
            if (_yellowTex != null) Destroy(_yellowTex);
        }

        private string _cachedLocalIP = "";

        private string GetLocalIP()
        {
            if (!string.IsNullOrEmpty(_cachedLocalIP)) return _cachedLocalIP;
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(ip))
                    {
                        _cachedLocalIP = ip.ToString();
                        return _cachedLocalIP;
                    }
                }
            }
            catch { }
            _cachedLocalIP = "DHCP";
            return _cachedLocalIP;
        }

        // ───────────────────────── State machine ─────────────────────
        private void EvaluateState()
        {
            float timeSince = udpReceiver.TimeSinceLastPacket;

            if (timeSince < connectionTimeout && udpReceiver.TotalPacketsReceived > 0)
            {
                State = ConnectionState.Receiving;
                _everReceivedPacket = true;
                StatusText = $"Connected ({udpReceiver.PacketsPerSecond} pps)";
            }
            else if (_everReceivedPacket)
            {
                State = ConnectionState.TimedOut;
                StatusText = $"Timed Out! (HL2 IP: {GetLocalIP()})";
            }
            else
            {
                State = ConnectionState.Disconnected;
                StatusText = $"Waiting for PC... (IP: {GetLocalIP()})";
            }

            // Fire transition events.
            if (State != _previousState)
            {
                if (State == ConnectionState.Receiving)
                {
                    Debug.Log("[NetworkManager] Connected to companion app.");
                    OnConnected?.Invoke();
                }
                else if (_previousState == ConnectionState.Receiving)
                {
                    Debug.LogWarning("[NetworkManager] Connection lost.");
                    OnDisconnected?.Invoke();
                }

                _previousState = State;
            }
        }

        // ───────────────────────── On-screen indicator ───────────────
        private void DrawConnectionIndicator()
        {
            EnsureGuiResources();

            float padding = 10f;
            float circleSize = 14f;
            float panelW = 320f;
            float panelH = 28f;
            float x = Screen.width - panelW - padding;
            float y = padding;

            // Background box.
            GUI.Box(new Rect(x, y, panelW, panelH), GUIContent.none);

            // Colored circle.
            Texture2D circleTex;
            switch (State)
            {
                case ConnectionState.Receiving: circleTex = _greenTex; break;
                case ConnectionState.TimedOut:  circleTex = _redTex;   break;
                default:                        circleTex = _yellowTex; break;
            }

            GUI.DrawTexture(
                new Rect(x + 6f, y + (panelH - circleSize) * 0.5f, circleSize, circleSize),
                circleTex);

            // Status label.
            GUI.Label(
                new Rect(x + 24f, y + 2f, panelW - 28f, panelH - 4f),
                StatusText, _labelStyle);
        }

        /// <summary>Lazily create GUI textures and styles so they're only allocated once.</summary>
        private void EnsureGuiResources()
        {
            if (_labelStyle != null) return;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _labelStyle.normal.textColor = Color.white;

            _greenTex  = MakeSolidTexture(Color.green);
            _redTex    = MakeSolidTexture(Color.red);
            _yellowTex = MakeSolidTexture(Color.yellow);
        }

        private static Texture2D MakeSolidTexture(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }
    }
}
