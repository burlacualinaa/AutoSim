// NetworkInputProvider.cs — IInputProvider that reads data from a UDPInputReceiver.
// Bridges network-received InputDataPackets into the vehicle input pipeline.
using UnityEngine;
using AutoSim.Networking;

namespace AutoSim.Input
{
    /// <summary>
    /// Network-based input provider that reads the latest <see cref="InputDataPacket"/>
    /// from a <see cref="UDPInputReceiver"/> component each frame. Designed to consume
    /// Logitech G920 wheel data forwarded from the PC companion app.
    /// </summary>
    [AddComponentMenu("AutoSim/Input/Network Input Provider")]
    public class NetworkInputProvider : MonoBehaviour, IInputProvider
    {
        // ---------------------------------------------------------------
        //  Serialized References
        // ---------------------------------------------------------------

        [Header("Network Receiver")]
        [Tooltip("UDPInputReceiver that receives InputDataPackets. Auto-found if left empty.")]
        [SerializeField] private UDPInputReceiver receiver;

        // ---------------------------------------------------------------
        //  IInputProvider Implementation
        // ---------------------------------------------------------------

        /// <inheritdoc/>
        public float Steering { get; private set; }

        /// <inheritdoc/>
        public float Throttle { get; private set; }

        /// <inheritdoc/>
        public float Brake { get; private set; }

        /// <inheritdoc/>
        public float Clutch { get; private set; }

        /// <inheritdoc/>
        public uint Buttons { get; private set; }

        /// <inheritdoc/>
        public bool IsConnected { get; private set; }

        /// <inheritdoc/>
        public string ProviderName => "Network (G920)";

        // ---------------------------------------------------------------
        //  Internal State
        // ---------------------------------------------------------------

        /// <summary>Time.realtimeSinceStartup when the last valid packet was received.</summary>
        private float lastPacketTime;

        /// <summary>Sequence number of the most recently consumed packet.</summary>
        private uint lastSequenceNumber;

        // ---------------------------------------------------------------
        //  Unity Lifecycle
        // ---------------------------------------------------------------

        private void Start()
        {
            // Auto-discover the receiver if not assigned in the Inspector
            if (receiver == null)
            {
                receiver = FindObjectOfType<UDPInputReceiver>();

                if (receiver == null)
                {
                    receiver = gameObject.AddComponent<UDPInputReceiver>();
                    Debug.Log($"[{ProviderName}] Auto-created UDPInputReceiver on {gameObject.name}");
                }
            }
        }

        private void Update()
        {
            if (receiver == null)
            {
                IsConnected = false;
                return;
            }

            // Read the latest packet from the receiver
            InputDataPacket packet = receiver.LatestPacket;

            // Only update if we have a new packet (sequence number changed)
            if (packet.sequenceNumber != lastSequenceNumber)
            {
                lastSequenceNumber = packet.sequenceNumber;
                lastPacketTime = Time.realtimeSinceStartup;

                Steering = packet.steering;
                Throttle = packet.throttle;
                Brake = packet.brake;
                Clutch = packet.clutch;
                Buttons = packet.buttons;
            }

            // Connection is alive if we received a packet within the timeout window
            float timeSinceLastPacket = Time.realtimeSinceStartup - lastPacketTime;
            IsConnected = timeSinceLastPacket < AutoSimConstants.CONNECTION_TIMEOUT_SECONDS;

            // Zero out inputs if connection is lost to prevent ghost inputs
            if (!IsConnected)
            {
                Steering = 0f;
                Throttle = 0f;
                Brake = 0f;
                Clutch = 0f;
                Buttons = 0;
            }
        }
    }
}
