// PedestrianCrossing.cs — IScenario implementation for a pedestrian crossing event.
// Creates crosswalk markings and a pedestrian figure that walks across the road when triggered.
using UnityEngine;

namespace AutoSim.Scenarios
{
    /// <summary>
    /// Simulates a pedestrian crossing scenario.
    /// A pedestrian waits on the sidewalk and begins crossing the road when the
    /// vehicle enters the trigger zone. Records reaction time, minimum distance,
    /// and collision status for research telemetry.
    /// </summary>
    [AddComponentMenu("AutoSim/Scenarios/Pedestrian Crossing")]
    public class PedestrianCrossing : MonoBehaviour, IScenario
    {
        // ──────────────────────────── Inspector Fields ────────────────────────────────

        [Header("Trigger Position")]
        [Tooltip("Z position along the road where the crosswalk is placed (metres).")]
        [SerializeField] private float triggerPositionZ = 200f;

        [Header("Road Geometry (should match ProceduralRoadGenerator)")]
        [Tooltip("Total road width including shoulders (metres).")]
        [SerializeField] private float roadWidth = 8f;

        [Tooltip("Distance from road edge to the sidewalk waiting position (metres).")]
        [SerializeField] private float sidewalkOffset = 3f;

        [Header("Pedestrian")]
        [Tooltip("Walking speed of the pedestrian (m/s).")]
        [SerializeField] private float walkSpeed = AutoSim.AutoSimConstants.PEDESTRIAN_WALK_SPEED;

        [Tooltip("If true, pedestrian crosses from right to left; otherwise left to right.")]
        [SerializeField] private bool crossRightToLeft = true;

        [Tooltip("Height of the pedestrian figure (metres).")]
        [SerializeField] private float pedestrianHeight = 1.7f;

        [Header("Crosswalk Markings")]
        [Tooltip("Number of white stripes across the crosswalk.")]
        [SerializeField] private int stripeCount = 8;

        [Tooltip("Width of each stripe (metres, along Z).")]
        [SerializeField] private float stripeWidth = 0.5f;

        [Tooltip("Gap between stripes (metres, along Z).")]
        [SerializeField] private float stripeGap = 0.5f;

        // ──────────────────────────── IScenario Properties ────────────────────────────

        public string ScenarioName => "Pedestrian Crossing";
        public ScenarioState State { get; private set; } = ScenarioState.Idle;
        public float TriggerPositionZ => triggerPositionZ;

        // ──────────────────────────── Runtime State ───────────────────────────────────

        private Transform _vehicleTransform;
        private Transform _scenarioContainer;

        // Pedestrian
        private Transform _pedestrian;
        private Vector3 _startPosition;
        private Vector3 _endPosition;
        private bool _pedestrianCrossed;

        // Metrics
        private float _triggerTime;       // Time.time when scenario was triggered
        private float _reactionTime;      // Time from trigger to first brake input
        private float _minDistance;        // Closest distance between vehicle and pedestrian
        private bool _collisionOccurred;
        private bool _brakeRecorded;

        // Collision detection radius (simplified sphere check)
        private const float COLLISION_RADIUS = 1.0f;

        // ──────────────────────────── IScenario Methods ───────────────────────────────

        /// <inheritdoc/>
        public void Initialize(Transform vehicleTransform)
        {
            _vehicleTransform = vehicleTransform;

            // Clean up previous
            ClearGenerated();

            _scenarioContainer = new GameObject("PedestrianCrossing_Scenario").transform;
            _scenarioContainer.SetParent(transform, false);

            CreateCrosswalkMarkings();
            CreatePedestrian();

            // Reset metrics
            _reactionTime = -1f;
            _minDistance = float.MaxValue;
            _collisionOccurred = false;
            _brakeRecorded = false;
            _pedestrianCrossed = false;

            State = ScenarioState.WaitingToTrigger;
            Debug.Log($"[PedestrianCrossing] Initialised at Z={triggerPositionZ}.");
        }

        /// <inheritdoc/>
        public void Trigger()
        {
            if (State != ScenarioState.WaitingToTrigger) return;

            _triggerTime = Time.time;
            State = ScenarioState.Active;
            Debug.Log("[PedestrianCrossing] Pedestrian begins crossing.");
        }

        /// <inheritdoc/>
        public void UpdateScenario()
        {
            if (State != ScenarioState.Active || _pedestrian == null) return;

            // ── Move pedestrian across the road ──
            if (!_pedestrianCrossed)
            {
                _pedestrian.position = Vector3.MoveTowards(
                    _pedestrian.position,
                    _endPosition,
                    walkSpeed * Time.deltaTime);

                if (Vector3.Distance(_pedestrian.position, _endPosition) < 0.1f)
                {
                    _pedestrianCrossed = true;
                    Debug.Log("[PedestrianCrossing] Pedestrian safely crossed.");
                }
            }

            // ── Track metrics ──
            float dist = Vector3.Distance(_vehicleTransform.position, _pedestrian.position);
            if (dist < _minDistance)
                _minDistance = dist;

            // Simplified collision check
            if (dist < COLLISION_RADIUS && !_collisionOccurred)
            {
                _collisionOccurred = true;
                Debug.LogWarning("[PedestrianCrossing] COLLISION with pedestrian!");
            }

            // Detect braking (check for brake input via Rigidbody deceleration or input)
            if (!_brakeRecorded)
            {
                Rigidbody rb = _vehicleTransform.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // Consider braking if the vehicle's forward velocity is decreasing
                    // (simplified: velocity dot forward < previous frame implies deceleration)
                    float forwardSpeed = Vector3.Dot(rb.velocity, _vehicleTransform.forward);
                    if (forwardSpeed < -0.1f || rb.velocity.magnitude < 0.5f)
                    {
                        _reactionTime = Time.time - _triggerTime;
                        _brakeRecorded = true;
                        Debug.Log($"[PedestrianCrossing] Brake detected. Reaction time: {_reactionTime:F2}s");
                    }
                }
            }

            // ── Completion check ──
            // Complete when pedestrian has crossed OR vehicle has passed well beyond the crosswalk
            float vehicleZ = _vehicleTransform.position.z;
            if (_pedestrianCrossed || vehicleZ > triggerPositionZ + 30f)
            {
                CompleteScenario();
            }
        }

        /// <inheritdoc/>
        public void Reset()
        {
            ClearGenerated();
            State = ScenarioState.Idle;
        }

        // ──────────────────────────── Metrics Access ──────────────────────────────────

        /// <summary>Reaction time in seconds from trigger to first brake input. -1 if no brake detected.</summary>
        public float ReactionTime => _reactionTime;

        /// <summary>Minimum distance (metres) between vehicle and pedestrian during the scenario.</summary>
        public float MinimumDistance => _minDistance;

        /// <summary>Whether a collision with the pedestrian occurred.</summary>
        public bool CollisionOccurred => _collisionOccurred;

        // ──────────────────────────── Visual Generation ───────────────────────────────

        private void CreateCrosswalkMarkings()
        {
            Transform markings = new GameObject("CrosswalkMarkings").transform;
            markings.SetParent(_scenarioContainer, false);

            Material stripeMat = CreateMaterial(Color.white, 0f, 0f);

            float totalMarkingWidth = stripeCount * stripeWidth + (stripeCount - 1) * stripeGap;
            float startZ = triggerPositionZ - totalMarkingWidth / 2f;

            for (int i = 0; i < stripeCount; i++)
            {
                float z = startZ + i * (stripeWidth + stripeGap) + stripeWidth / 2f;

                // Each stripe spans the full road width
                GameObject stripe = CreateFlatQuad(
                    $"Stripe_{i}",
                    new Vector3(0f, 0.015f, z),
                    roadWidth, stripeWidth);
                stripe.GetComponent<MeshRenderer>().sharedMaterial = stripeMat;
                stripe.transform.SetParent(markings, false);
            }
        }

        private void CreatePedestrian()
        {
            _pedestrian = new GameObject("Pedestrian").transform;
            _pedestrian.SetParent(_scenarioContainer, false);

            float halfRoad = roadWidth / 2f;

            // Starting side
            float startX = crossRightToLeft ? (halfRoad + sidewalkOffset) : -(halfRoad + sidewalkOffset);
            float endX = crossRightToLeft ? -(halfRoad + sidewalkOffset) : (halfRoad + sidewalkOffset);

            _startPosition = new Vector3(startX, 0f, triggerPositionZ);
            _endPosition = new Vector3(endX, 0f, triggerPositionZ);
            _pedestrian.position = _startPosition;

            // Body (capsule)
            float bodyHeight = pedestrianHeight * 0.75f;
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(_pedestrian, false);
            body.transform.localPosition = new Vector3(0f, bodyHeight / 2f, 0f);
            body.transform.localScale = new Vector3(0.35f, bodyHeight / 2f, 0.35f);
            ApplyPedestrianMaterial(body, new Color(0.3f, 0.35f, 0.7f)); // blue-ish clothing

            // Head (sphere)
            float headRadius = 0.12f;
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(_pedestrian, false);
            head.transform.localPosition = new Vector3(0f, bodyHeight + headRadius, 0f);
            head.transform.localScale = Vector3.one * headRadius * 2f;
            ApplyPedestrianMaterial(head, new Color(0.85f, 0.72f, 0.6f)); // skin tone
        }

        // ──────────────────────────── Helpers ─────────────────────────────────────────

        private void CompleteScenario()
        {
            State = ScenarioState.Completed;
            Debug.Log($"[PedestrianCrossing] Scenario complete. " +
                      $"Reaction: {(_reactionTime >= 0 ? $"{_reactionTime:F2}s" : "N/A")}, " +
                      $"MinDist: {_minDistance:F2}m, Collision: {_collisionOccurred}");
        }

        private void ClearGenerated()
        {
            if (_scenarioContainer != null)
            {
                if (Application.isPlaying)
                    Destroy(_scenarioContainer.gameObject);
                else
                    DestroyImmediate(_scenarioContainer.gameObject);
                _scenarioContainer = null;
            }
            _pedestrian = null;
        }

        /// <summary>Creates a flat quad lying on the XZ plane.</summary>
        private static GameObject CreateFlatQuad(string name, Vector3 position, float width, float length)
        {
            // Re-use the helper from ProceduralRoadGenerator
            GameObject quad = AutoSim.Environment.ProceduralRoadGenerator.CreateQuadMesh(position, width, length);
            quad.name = name;
            return quad;
        }

        private static Material CreateMaterial(Color color, float metallic, float glossiness)
        {
            Material mat = new Material(Shader.Find("Standard"))
            {
                color = color
            };
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Glossiness", glossiness);
            return mat;
        }

        private static void ApplyPedestrianMaterial(GameObject go, Color color)
        {
            Material mat = CreateMaterial(color, 0f, 0.15f);
            go.GetComponent<MeshRenderer>().material = mat;
        }
    }
}
