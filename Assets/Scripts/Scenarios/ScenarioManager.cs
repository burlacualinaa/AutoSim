// ScenarioManager.cs — Manages driving test scenarios triggered by vehicle position.
// Defines the IScenario interface and ScenarioState enum used by all scenario implementations.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoSim.Scenarios
{
    // ──────────────────────────── Shared Types ────────────────────────────────────

    /// <summary>The lifecycle state of a scenario.</summary>
    public enum ScenarioState
    {
        /// <summary>Scenario is idle and has not been initialised for triggering.</summary>
        Idle,

        /// <summary>Scenario is initialised and waiting for the vehicle to reach the trigger zone.</summary>
        WaitingToTrigger,

        /// <summary>Scenario is currently active (hazard visible, pedestrian walking, etc.).</summary>
        Active,

        /// <summary>Scenario has finished (safely passed, collision, or timed out).</summary>
        Completed
    }

    /// <summary>
    /// Common contract for all driving-test scenarios.
    /// Implement this interface on MonoBehaviours that represent specific hazard/event types.
    /// </summary>
    public interface IScenario
    {
        /// <summary>Human-readable name for logging and UI.</summary>
        string ScenarioName { get; }

        /// <summary>Current lifecycle state.</summary>
        ScenarioState State { get; }

        /// <summary>
        /// One-time setup. Creates visuals and resets internal state.
        /// </summary>
        /// <param name="vehicleTransform">The transform of the player vehicle.</param>
        void Initialize(Transform vehicleTransform);

        /// <summary>Activates the scenario (e.g. makes hazard visible, starts pedestrian).</summary>
        void Trigger();

        /// <summary>Called every frame while the scenario is active.</summary>
        void UpdateScenario();

        /// <summary>Resets the scenario back to <see cref="ScenarioState.Idle"/>.</summary>
        void Reset();

        /// <summary>Z position along the road at which this scenario triggers.</summary>
        float TriggerPositionZ { get; }
    }

    // ──────────────────────────── Scenario Manager ────────────────────────────────

    /// <summary>
    /// Manages a sequence of <see cref="IScenario"/> instances, triggering them
    /// as the vehicle progresses along the road.
    /// </summary>
    [AddComponentMenu("AutoSim/Scenarios/Scenario Manager")]
    public class ScenarioManager : MonoBehaviour
    {
        // ──────────────────────────── Inspector Fields ────────────────────────────────

        [Header("Vehicle")]
        [Tooltip("The vehicle transform to track. If null, searched by tag 'Player'.")]
        [SerializeField] private Transform vehicleTransform;

        [Header("Trigger Settings")]
        [Tooltip("Distance (metres) ahead of trigger Z at which to activate the scenario.")]
        [SerializeField] private float triggerAheadDistance = 80f;

        // ──────────────────────────── Events ──────────────────────────────────────────

        /// <summary>Fired when a scenario is triggered. Parameter: scenario name.</summary>
        public event Action<string> OnScenarioTriggered;

        /// <summary>Fired when a scenario completes. Parameter: scenario name.</summary>
        public event Action<string> OnScenarioCompleted;

        // ──────────────────────────── Runtime State ───────────────────────────────────

        /// <summary>All registered scenarios, sorted by trigger Z.</summary>
        private readonly List<IScenario> _scenarios = new List<IScenario>();

        /// <summary>Index of the next scenario that hasn't been triggered yet.</summary>
        private int _nextScenarioIndex;

        /// <summary>Whether a scenario sequence is running.</summary>
        private bool _sequenceRunning;

        // ──────────────────────────── Properties ──────────────────────────────────────

        /// <summary>Current vehicle speed in m/s (magnitude of velocity on XZ plane).</summary>
        public float VehicleSpeedMps { get; private set; }

        /// <summary>Current vehicle speed in km/h.</summary>
        public float VehicleSpeedKmh => VehicleSpeedMps * 3.6f;

        // ──────────────────────────── Unity Lifecycle ─────────────────────────────────

        private void Start()
        {
            if (vehicleTransform == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                    vehicleTransform = player.transform;
            }

            // Auto-discover IScenario components on children
            IScenario[] found = GetComponentsInChildren<IScenario>(includeInactive: true);
            foreach (var s in found)
            {
                if (!_scenarios.Contains(s))
                    _scenarios.Add(s);
            }

            // Sort by trigger position (ascending Z)
            _scenarios.Sort((a, b) => a.TriggerPositionZ.CompareTo(b.TriggerPositionZ));

            Debug.Log($"[ScenarioManager] Discovered {_scenarios.Count} scenario(s).");
        }

        private void Update()
        {
            if (!_sequenceRunning || vehicleTransform == null) return;

            UpdateVehicleSpeed();
            CheckTriggers();
            UpdateActiveScenarios();
        }

        // ──────────────────────────── Public API ──────────────────────────────────────

        /// <summary>
        /// Begins the scenario sequence. All registered scenarios are initialised
        /// and set to <see cref="ScenarioState.WaitingToTrigger"/>.
        /// </summary>
        public void StartScenarioSequence()
        {
            if (vehicleTransform == null)
            {
                Debug.LogError("[ScenarioManager] Cannot start — no vehicle transform assigned.");
                return;
            }

            foreach (var s in _scenarios)
            {
                s.Initialize(vehicleTransform);
            }

            _nextScenarioIndex = 0;
            _sequenceRunning = true;
            Debug.Log("[ScenarioManager] Scenario sequence started.");
        }

        /// <summary>
        /// Resets all scenarios back to <see cref="ScenarioState.Idle"/> and stops the sequence.
        /// </summary>
        public void ResetAllScenarios()
        {
            _sequenceRunning = false;
            _nextScenarioIndex = 0;

            foreach (var s in _scenarios)
                s.Reset();

            Debug.Log("[ScenarioManager] All scenarios reset.");
        }

        /// <summary>Returns the name of the currently active scenario, or null if none.</summary>
        public string GetActiveScenarioName()
        {
            foreach (var s in _scenarios)
            {
                if (s.State == ScenarioState.Active)
                    return s.ScenarioName;
            }
            return null;
        }

        /// <summary>Manually register a scenario at runtime.</summary>
        public void RegisterScenario(IScenario scenario)
        {
            if (!_scenarios.Contains(scenario))
            {
                _scenarios.Add(scenario);
                _scenarios.Sort((a, b) => a.TriggerPositionZ.CompareTo(b.TriggerPositionZ));
            }
        }

        // ──────────────────────────── Internal Logic ──────────────────────────────────

        private void UpdateVehicleSpeed()
        {
            Rigidbody rb = vehicleTransform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 vel = rb.velocity;
                vel.y = 0f;
                VehicleSpeedMps = vel.magnitude;
            }
        }

        /// <summary>Checks if the vehicle has reached the trigger zone of the next scenario.</summary>
        private void CheckTriggers()
        {
            if (_nextScenarioIndex >= _scenarios.Count) return;

            IScenario next = _scenarios[_nextScenarioIndex];

            // Trigger when vehicle is within triggerAheadDistance of the scenario's Z position
            float vehicleZ = vehicleTransform.position.z;
            float triggerThreshold = next.TriggerPositionZ - triggerAheadDistance;

            if (vehicleZ >= triggerThreshold && next.State == ScenarioState.WaitingToTrigger)
            {
                Debug.Log($"[ScenarioManager] Triggering scenario: {next.ScenarioName} at Z={vehicleZ:F1}");
                next.Trigger();
                OnScenarioTriggered?.Invoke(next.ScenarioName);
                _nextScenarioIndex++;
            }
        }

        /// <summary>Updates all active scenarios and fires completion events.</summary>
        private void UpdateActiveScenarios()
        {
            foreach (var s in _scenarios)
            {
                if (s.State == ScenarioState.Active)
                {
                    s.UpdateScenario();

                    // Check if the scenario just completed
                    if (s.State == ScenarioState.Completed)
                    {
                        Debug.Log($"[ScenarioManager] Scenario completed: {s.ScenarioName}");
                        OnScenarioCompleted?.Invoke(s.ScenarioName);
                    }
                }
            }
        }
    }
}
