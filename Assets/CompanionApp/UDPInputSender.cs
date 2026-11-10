// UDPInputSender.cs — Reads Logitech G920 input via the Logitech SDK and sends
// InputDataPackets over UDP to the HoloLens 2 at a configurable rate.
// Desktop-only: excluded from UWP builds.
#if !UNITY_WSA

using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using AutoSim.Networking;

namespace AutoSim.CompanionApp
{
    /// <summary>
    /// Reads the Logitech G920 steering wheel and pedals, constructs an
    /// <see cref="InputDataPacket"/>, and transmits it over UDP to the target
    /// HoloLens 2 application at a configurable rate (default 60 Hz).
    /// </summary>
    [AddComponentMenu("AutoSim/CompanionApp/UDP Input Sender")]
    public class UDPInputSender : MonoBehaviour
    {
        // ───────────────────────── Inspector ─────────────────────────
        [Header("Target")]
        [Tooltip("IP address of the HoloLens 2 device.")]
        [SerializeField] private string targetIP = "192.168.1.100";

        [Tooltip("UDP port on the HoloLens 2 (must match UDPInputReceiver).")]
        [SerializeField] private int targetPort = AutoSimConstants.DEFAULT_UDP_PORT;

        [Header("Steering Wheel")]
        [Tooltip("Logitech SDK device index (0 = first wheel).")]
        [SerializeField] private int deviceIndex = 0;

        [Tooltip("Desired wheel rotation range in degrees (e.g., 900).")]
        [SerializeField] private int steeringRange = 900;

        [Header("Send Rate")]
        [Tooltip("How many packets per second to send.")]
        [SerializeField] private float sendRateHz = AutoSimConstants.SEND_RATE_HZ;

        [Header("UI")]
        [Tooltip("Show debug GUI overlay with input values.")]
        [SerializeField] private bool showDebugGUI = true;

        // ───────────────────────── Public API ────────────────────────
        /// <summary>Whether the Logitech SDK was initialized successfully.</summary>
        public bool IsSDKInitialized { get; private set; }

        /// <summary>Whether a G920 wheel is currently connected.</summary>
        public bool IsWheelConnected { get; private set; }

        /// <summary>Whether this sender is actively transmitting packets.</summary>
        public bool IsSending { get; set; } = true;

        /// <summary>Total number of packets sent since startup.</summary>
        public long PacketsSent { get; private set; }

        /// <summary>The most recently sent packet (for debug display).</summary>
        public InputDataPacket LastSentPacket { get; private set; }

        /// <summary>Allows external code to set the target IP at runtime.</summary>
        public string TargetIP
        {
            get => targetIP;
            set => targetIP = value;
        }

        /// <summary>Allows external code to set the target port at runtime.</summary>
        public int TargetPort
        {
            get => targetPort;
            set => targetPort = value;
        }

        // ───────────────────────── Private ───────────────────────────
        private UdpClient _udpClient;
        private IPEndPoint _remoteEndPoint;
        private uint _sequenceNumber;
        private float _sendInterval;
        private float _sendTimer;

        // ───────────────────────── Unity lifecycle ───────────────────
        private void Start()
        {
            InitializeSDK();
            InitializeNetwork();
            _sendInterval = 1f / Mathf.Max(sendRateHz, 1f);
        }

        private void Update()
        {
            // Always poll the SDK so the wheel stays responsive.
            if (IsSDKInitialized)
            {
                LogitechSDKWrapper.LogiUpdate();
                IsWheelConnected = LogitechSDKWrapper.LogiIsConnected(deviceIndex);
            }

            if (!IsSending || !IsWheelConnected) return;

            // Throttle send rate.
            _sendTimer += Time.unscaledDeltaTime;
            if (_sendTimer < _sendInterval) return;
            _sendTimer -= _sendInterval;

            SendPacket();
        }

        private void OnDestroy()
        {
            ShutdownSDK();
            ShutdownNetwork();
        }

        // ───────────────────────── SDK init / shutdown ───────────────
        private void InitializeSDK()
        {
            try
            {
                IsSDKInitialized = LogitechSDKWrapper.LogiSteeringInitialize(false);
                if (IsSDKInitialized)
                {
                    Debug.Log("[UDPInputSender] Logitech SDK initialized.");
                    LogitechSDKWrapper.LogiSetOperatingRange(deviceIndex, steeringRange);
                }
                else
                {
                    Debug.LogWarning("[UDPInputSender] Logitech SDK failed to initialize. " +
                                     "Make sure LogitechSteeringWheelEnginesWrapper.dll is present.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UDPInputSender] SDK init exception: {ex.Message}");
                IsSDKInitialized = false;
            }
        }

        private void ShutdownSDK()
        {
            if (!IsSDKInitialized) return;
            try
            {
                LogitechSDKWrapper.LogiSteeringShutdown();
                Debug.Log("[UDPInputSender] Logitech SDK shut down.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UDPInputSender] SDK shutdown error: {ex.Message}");
            }
            IsSDKInitialized = false;
        }

        // ───────────────────────── Network init / shutdown ───────────
        private void InitializeNetwork()
        {
            try
            {
                _udpClient = new UdpClient();
                UpdateRemoteEndPoint();
                Debug.Log($"[UDPInputSender] Will send to {targetIP}:{targetPort}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UDPInputSender] Network init failed: {ex.Message}");
            }
        }

        private void ShutdownNetwork()
        {
            try
            {
                _udpClient?.Close();
                _udpClient?.Dispose();
                _udpClient = null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UDPInputSender] Network shutdown error: {ex.Message}");
            }
        }

        /// <summary>
        /// (Re)builds the <see cref="IPEndPoint"/> from the current
        /// <see cref="targetIP"/> and <see cref="targetPort"/> strings.
        /// </summary>
        public void UpdateRemoteEndPoint()
        {
            try
            {
                _remoteEndPoint = new IPEndPoint(IPAddress.Parse(targetIP), targetPort);
            }
            catch (FormatException)
            {
                Debug.LogWarning($"[UDPInputSender] Invalid IP address: {targetIP}");
            }
        }

        // ───────────────────────── Packet construction & send ────────
        private void SendPacket()
        {
            if (_udpClient == null || _remoteEndPoint == null) return;

            LogitechSDKWrapper.DIJOYSTATE2? stateNullable =
                LogitechSDKWrapper.GetState(deviceIndex);

            if (!stateNullable.HasValue) return;

            LogitechSDKWrapper.DIJOYSTATE2 state = stateNullable.Value;

            _sequenceNumber++;

            InputDataPacket packet = new InputDataPacket
            {
                sequenceNumber = _sequenceNumber,
                steering       = LogitechSDKWrapper.NormalizeSteering(state.lX),
                throttle       = LogitechSDKWrapper.NormalizePedal(state.lY),
                brake          = LogitechSDKWrapper.NormalizePedal(state.lRz),
                clutch         = LogitechSDKWrapper.NormalizePedal(state.lZ),
                buttons        = LogitechSDKWrapper.PackButtons(state),
                timestamp      = Time.unscaledTime
            };

            try
            {
                byte[] data = packet.Serialize();
                _udpClient.Send(data, data.Length, _remoteEndPoint);
                PacketsSent++;
                LastSentPacket = packet;
            }
            catch (SocketException ex)
            {
                Debug.LogWarning($"[UDPInputSender] Send failed: {ex.SocketErrorCode}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UDPInputSender] Send error: {ex.Message}");
            }
        }

        // ───────────────────────── Debug GUI ─────────────────────────
        private void OnGUI()
        {
            if (!showDebugGUI) return;

            float x = 10f;
            float y = 10f;
            float w = 340f;
            float lineH = 22f;
            int line = 0;

            GUI.Box(new Rect(x, y, w, lineH * 10), "G920 UDP Sender");

            line = 1;
            GUI.Label(R(), $"SDK Init: {IsSDKInitialized}");
            GUI.Label(R(), $"Wheel Connected: {IsWheelConnected}");
            GUI.Label(R(), $"Sending: {IsSending}");
            GUI.Label(R(), $"Target: {targetIP}:{targetPort}");
            GUI.Label(R(), $"Packets Sent: {PacketsSent}");
            GUI.Label(R(), $"Steer: {LastSentPacket.steering:F3}");
            GUI.Label(R(), $"Throttle: {LastSentPacket.throttle:F3}  " +
                           $"Brake: {LastSentPacket.brake:F3}");
            GUI.Label(R(), $"Clutch: {LastSentPacket.clutch:F3}  " +
                           $"Buttons: 0x{LastSentPacket.buttons:X4}");
            GUI.Label(R(), $"Seq: {LastSentPacket.sequenceNumber}");

            Rect R()
            {
                return new Rect(x + 6f, y + lineH * line++, w - 12f, lineH);
            }
        }
    }
}

#endif // !UNITY_WSA
