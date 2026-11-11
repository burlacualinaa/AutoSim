// TelemetryRecorder.cs — 20 Hz driving and telemetry logger saving directly to CSV.
// Follows research protocol: 20 Hz fixed recording rate, preserving exact CSV schema and headers.
using System;
using System.IO;
using UnityEngine;
using AutoSim.Input;
using AutoSim.Vehicle;
using AutoSim.Networking;
using AutoSim.Scenarios;

namespace AutoSim.Telemetry
{
    /// <summary>
    /// Records vehicle dynamics, driver inputs, and telemetry at fixed 20 Hz into CSV.
    /// Toggle recording with [F5].
    /// </summary>
    [AddComponentMenu("AutoSim/Telemetry/Telemetry Recorder")]
    public class TelemetryRecorder : MonoBehaviour
    {
        // ──────────────────────────── Serialized References ───────────────────────────

        [Header("Data Sources (Auto-discovered if null)")]
        [SerializeField] private VehicleController vehicleController;
        [SerializeField] private UDPInputReceiver udpReceiver;
        [SerializeField] private ScenarioManager scenarioManager;

        [Header("Recording Configuration")]
        [Tooltip("Participant identifier prefix for the CSV file.")]
        [SerializeField] private string participantId = "P001";

        [Tooltip("Fixed recording frequency in Hertz (protocol standard: 20 Hz).")]
        [SerializeField] private float recordRateHz = AutoSimConstants.TELEMETRY_RECORD_HZ; // 20 Hz

        [Tooltip("Automatically start recording on scene load.")]
        [SerializeField] private bool autoRecordOnStart = false;

        // ──────────────────────────── Public State ────────────────────────────────────

        public bool IsRecording { get; private set; } = false;
        public int RecordedFrameCount { get; private set; } = 0;
        public double ElapsedSessionTime { get; private set; } = 0.0;
        public string ActiveFilePath { get; private set; } = "";

        // ──────────────────────────── Internal State ──────────────────────────────────

        private StreamWriter writer;
        private float sampleTimer = 0f;
        private Vector3 lastVelocity = Vector3.zero;
        private float lastSampleTime = 0f;

        // ──────────────────────────── Unity Lifecycle ─────────────────────────────────

        private void Start()
        {
            if (vehicleController == null)
                vehicleController = FindObjectOfType<VehicleController>();

            if (udpReceiver == null)
                udpReceiver = FindObjectOfType<UDPInputReceiver>();

            if (scenarioManager == null)
                scenarioManager = FindObjectOfType<ScenarioManager>();

            if (autoRecordOnStart)
            {
                StartRecording(participantId);
            }
        }

        private void Update()
        {
            // Toggle recording with F5
            if (UnityEngine.Input.GetKeyDown(KeyCode.F5))
            {
                if (IsRecording) StopRecording();
                else StartRecording(participantId);
            }

            if (!IsRecording) return;

            float dt = Time.unscaledDeltaTime;
            ElapsedSessionTime += dt;
            sampleTimer += dt;

            float interval = 1.0f / Mathf.Max(1f, recordRateHz); // 0.05s for 20 Hz
            if (sampleTimer >= interval)
            {
                sampleTimer -= interval;
                RecordFrame();
            }
        }

        private void OnDestroy()
        {
            if (IsRecording)
            {
                StopRecording();
            }
        }

        // ──────────────────────────── Recording Control ───────────────────────────────

        /// <summary>
        /// Starts recording telemetry at 20 Hz. Creates CSV in Application.persistentDataPath.
        /// </summary>
        public void StartRecording(string id)
        {
            if (IsRecording) return;

            participantId = string.IsNullOrEmpty(id) ? "Participant" : id;
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
            string filename = $"AutoSim_{participantId}_{timestamp}.csv";
            ActiveFilePath = Path.Combine(Application.persistentDataPath, filename);

            try
            {
                writer = new StreamWriter(ActiveFilePath, false, System.Text.Encoding.UTF8);
                writer.WriteLine(TelemetryFrame.CsvHeader());
                writer.Flush();

                IsRecording = true;
                RecordedFrameCount = 0;
                ElapsedSessionTime = 0.0;
                lastSampleTime = Time.time;

                Debug.Log($"[TelemetryRecorder] Started 20 Hz recording to: {ActiveFilePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TelemetryRecorder] Failed to start CSV recording: {ex.Message}");
                IsRecording = false;
            }
        }

        /// <summary>
        /// Stops recording and flushes the CSV file to disk.
        /// </summary>
        public void StopRecording()
        {
            if (!IsRecording) return;

            try
            {
                if (writer != null)
                {
                    writer.Flush();
                    writer.Close();
                    writer = null;
                }
                Debug.Log($"[TelemetryRecorder] Stopped recording. Saved {RecordedFrameCount} frames to: {ActiveFilePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TelemetryRecorder] Error closing CSV writer: {ex.Message}");
            }
            finally
            {
                IsRecording = false;
            }
        }

        // ──────────────────────────── Frame Capture (20 Hz) ───────────────────────────

        private void RecordFrame()
        {
            if (writer == null) return;

            TelemetryFrame frame = new TelemetryFrame();
            frame.sessionTime = ElapsedSessionTime;
            frame.frameNumber = RecordedFrameCount++;
            frame.systemTimestamp = DateTime.UtcNow.ToString("o");

            // Driver inputs
            IInputProvider input = vehicleController != null ? vehicleController.ActiveInputProvider : null;
            if (input != null)
            {
                frame.steering = input.Steering;
                frame.throttle = input.Throttle;
                frame.brake = input.Brake;
                frame.clutch = input.Clutch;
            }

            // Vehicle dynamics
            if (vehicleController != null)
            {
                Transform vTransform = vehicleController.transform;
                Vector3 pos = vTransform.position;
                Vector3 rot = vTransform.eulerAngles;

                frame.posX = pos.x;
                frame.posY = pos.y;
                frame.posZ = pos.z;
                frame.rotX = rot.x;
                frame.rotY = rot.y;
                frame.rotZ = rot.z;

                frame.speedKmh = vehicleController.SpeedKmh;

                // Acceleration & lateral G
                Rigidbody rb = vehicleController.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    float deltaT = Mathf.Max(0.001f, Time.time - lastSampleTime);
                    Vector3 accel = (rb.velocity - lastVelocity) / deltaT;
                    lastVelocity = rb.velocity;
                    lastSampleTime = Time.time;

                    frame.accelerationMps2 = accel.magnitude;
                    frame.lateralG = Vector3.Dot(accel, vTransform.right) / 9.81f;
                }

                // Wheels RPM
                frame.wheelRpmFL = vehicleController.GetWheelRpm(0);
                frame.wheelRpmFR = vehicleController.GetWheelRpm(1);
                frame.wheelRpmRL = vehicleController.GetWheelRpm(2);
                frame.wheelRpmRR = vehicleController.GetWheelRpm(3);
            }

            // Eye tracking defaults (or HoloLens / simulated gaze)
            frame.gazeValid = false;
            frame.gazedObjectName = "None";
            frame.gazeHitDistance = 0f;

            // Network telemetry
            if (udpReceiver != null)
            {
                frame.networkPacketsReceived = udpReceiver.TotalPacketsReceived;
                frame.networkLatencyMs = udpReceiver.TimeSinceLastPacket * 1000f;
            }

            // Scenario tracking
            if (scenarioManager != null)
            {
                frame.activeScenario = scenarioManager.GetActiveScenarioName() ?? "None";
                frame.distanceToHazard = 999f;
            }
            else
            {
                frame.activeScenario = "None";
                frame.distanceToHazard = 999f;
            }

            // Write row (invariant culture formatted in TelemetryFrame)
            try
            {
                writer.WriteLine(frame.ToCsvRow());

                // Periodic flush every ~100 frames (every 5 seconds at 20 Hz)
                if (RecordedFrameCount % 100 == 0)
                {
                    writer.Flush();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TelemetryRecorder] Write row failed: {ex.Message}");
            }
        }
    }
}
