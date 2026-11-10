// UDPInputReceiver.cs — Receives InputDataPackets over UDP on the HoloLens 2.
// NetworkInputProvider reads LatestPacket from this component each frame.
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace AutoSim.Networking
{
    /// <summary>
    /// Listens for <see cref="InputDataPacket"/> datagrams on a UDP port.
    /// Runs a background receive thread and exposes the most recent packet
    /// via <see cref="LatestPacket"/> for consumption by <see cref="AutoSim.Input.NetworkInputProvider"/>.
    /// </summary>
    [AddComponentMenu("AutoSim/Networking/UDP Input Receiver")]
    public class UDPInputReceiver : MonoBehaviour
    {
        // ---------------------------------------------------------------
        //  Configuration
        // ---------------------------------------------------------------

        [Header("Network Settings")]
        [Tooltip("UDP port to listen on. Must match the companion app sender port.")]
        [SerializeField] private int listenPort = AutoSimConstants.DEFAULT_UDP_PORT;

        // ---------------------------------------------------------------
        //  Public API
        // ---------------------------------------------------------------

        /// <summary>The most recently received and validated packet. Thread-safe read.</summary>
        public InputDataPacket LatestPacket
        {
            get
            {
                lock (packetLock)
                {
                    return latestPacket;
                }
            }
        }

        /// <summary>Total number of valid packets received since start.</summary>
        public int TotalPacketsReceived => totalPacketsReceived;

        /// <summary>Elapsed time in seconds since the last valid packet was received.</summary>
        public float TimeSinceLastPacket => lastReceiveTime > 0f ? Time.realtimeSinceStartup - lastReceiveTime : float.MaxValue;

        /// <summary>Calculated throughput in packets per second.</summary>
        public int PacketsPerSecond => packetsPerSecond;

        /// <summary>True if receiving packets within connection timeout.</summary>
        public bool IsReceiving => TimeSinceLastPacket < AutoSimConstants.CONNECTION_TIMEOUT_SECONDS && totalPacketsReceived > 0;

        // ---------------------------------------------------------------
        //  Internal State
        // ---------------------------------------------------------------

        private UdpClient udpClient;
        private Thread receiveThread;
        private volatile bool isRunning;

        private InputDataPacket latestPacket;
        private readonly object packetLock = new object();
        private int totalPacketsReceived;
        private float lastReceiveTime;
        private int packetsPerSecond;
        private int ppsCounter;
        private float ppsTimer;

        // ---------------------------------------------------------------
        //  Unity Lifecycle
        // ---------------------------------------------------------------

        private void OnEnable()
        {
            StartListening();
        }

        private void OnDisable()
        {
            StopListening();
        }

        private int lastSeenPacketCount;

        private void Update()
        {
            if (totalPacketsReceived != lastSeenPacketCount)
            {
                ppsCounter += (totalPacketsReceived - lastSeenPacketCount);
                lastSeenPacketCount = totalPacketsReceived;
                lastReceiveTime = Time.realtimeSinceStartup;
            }

            ppsTimer += Time.deltaTime;
            if (ppsTimer >= 1.0f)
            {
                packetsPerSecond = ppsCounter;
                ppsCounter = 0;
                ppsTimer = 0f;
            }
        }

        // ---------------------------------------------------------------
        //  Networking
        // ---------------------------------------------------------------

        private void StartListening()
        {
            if (isRunning)
                return;

            try
            {
                udpClient = new UdpClient(listenPort);
                udpClient.Client.ReceiveTimeout = 1000; // 1s timeout for clean shutdown
                isRunning = true;

                receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "AutoSim-UDPReceiver"
                };
                receiveThread.Start();

                Debug.Log($"[UDPInputReceiver] Listening on port {listenPort}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UDPInputReceiver] Failed to start: {ex.Message}");
            }
        }

        private void StopListening()
        {
            isRunning = false;

            try
            {
                udpClient?.Close();
            }
            catch (Exception) { /* Suppress close exceptions */ }

            if (receiveThread != null && receiveThread.IsAlive)
            {
                receiveThread.Join(2000);
            }

            receiveThread = null;
            udpClient = null;

            Debug.Log("[UDPInputReceiver] Stopped.");
        }

        /// <summary>
        /// Background receive loop. Runs on a dedicated thread.
        /// </summary>
        private void ReceiveLoop()
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

            while (isRunning)
            {
                try
                {
                    byte[] data = udpClient.Receive(ref remoteEP);

                    if (InputDataPacket.TryDeserialize(data, out InputDataPacket packet))
                    {
                        lock (packetLock)
                        {
                            latestPacket = packet;
                        }

                        Interlocked.Increment(ref totalPacketsReceived);
                    }
                }
                catch (SocketException)
                {
                    // Timeout or socket closed — expected during shutdown
                }
                catch (ObjectDisposedException)
                {
                    // Socket disposed during shutdown
                    break;
                }
            }
        }
    }
}
