// HazardScenario.cs — IScenario implementation for unexpected road hazards.
// Supports multiple hazard types: fallen tree, stopped vehicle, debris, and animal crossing.
using UnityEngine;

namespace AutoSim.Scenarios
{
    /// <summary>Type of road hazard to spawn.</summary>
    public enum HazardType
    {
        FallenTree,
        StoppedVehicle,
        Debris,
        AnimalCrossing
    }

    /// <summary>
    /// Simulates an unexpected road hazard that appears when the vehicle is within range.
    /// Records reaction time, braking distance, and minimum distance for research telemetry.
    /// </summary>
    [AddComponentMenu("AutoSim/Scenarios/Hazard Scenario")]
    public class HazardScenario : MonoBehaviour, IScenario
    {
        // ──────────────────────────── Inspector Fields ────────────────────────────────

        [Header("Hazard Configuration")]
        [Tooltip("Type of hazard to place on the road.")]
        [SerializeField] private HazardType hazardType = HazardType.FallenTree;

        [Tooltip("Z position along the road where the hazard is placed (metres).")]
        [SerializeField] private float triggerPositionZ = 350f;

        [Header("Road Geometry")]
        [Tooltip("Total road width including shoulders (metres).")]
        [SerializeField] private float roadWidth = 8f;

        [Tooltip("Width of a single lane (metres). Hazard is placed in the driving lane.")]
        [SerializeField] private float laneWidth = 3.5f;

        [Header("Animal Crossing")]
        [Tooltip("Speed of the crossing animal (m/s).")]
        [SerializeField] private float animalSpeed = 3.5f;

        [Tooltip("Distance from road edge where the animal starts and ends.")]
        [SerializeField] private float animalRunOffset = 5f;

        [Header("Blinking Light (Stopped Vehicle)")]
        [Tooltip("Blink interval in seconds for the stopped vehicle's hazard light.")]
        [SerializeField] private float blinkInterval = 0.5f;

        // ──────────────────────────── IScenario Properties ────────────────────────────

        public string ScenarioName => $"Hazard: {hazardType}";
        public ScenarioState State { get; private set; } = ScenarioState.Idle;
        public float TriggerPositionZ => triggerPositionZ;

        // ──────────────────────────── Runtime State ───────────────────────────────────

        private Transform _vehicleTransform;
        private Transform _scenarioContainer;
        private GameObject _hazardRoot;

        // Animal crossing
        private Transform _animal;
        private Vector3 _animalStart;
        private Vector3 _animalEnd;
        private bool _animalCrossed;

        // Stopped vehicle blinking
        private MeshRenderer _blinkLight;
        private float _blinkTimer;
        private bool _blinkOn;

        // Metrics
        private float _triggerTime;
        private float _reactionTime = -1f;
        private float _minDistance = float.MaxValue;
        private float _brakingDistance;
        private float _speedAtTrigger;
        private bool _brakeRecorded;
        private Vector3 _brakeStartPos;

        // Collision detection
        private const float COLLISION_RADIUS = 1.5f;

        // ──────────────────────────── IScenario Methods ───────────────────────────────

        /// <inheritdoc/>
        public void Initialize(Transform vehicleTransform)
        {
            _vehicleTransform = vehicleTransform;
            ClearGenerated();

            _scenarioContainer = new GameObject($"HazardScenario_{hazardType}").transform;
            _scenarioContainer.SetParent(transform, false);

            CreateHazard();

            // Hide until triggered (except the hazard area — it appears suddenly)
            if (_hazardRoot != null)
                _hazardRoot.SetActive(false);

            // Reset metrics
            _reactionTime = -1f;
            _minDistance = float.MaxValue;
            _brakingDistance = 0f;
            _speedAtTrigger = 0f;
            _brakeRecorded = false;
            _animalCrossed = false;

            State = ScenarioState.WaitingToTrigger;
            Debug.Log($"[HazardScenario] Initialised: {hazardType} at Z={triggerPositionZ}.");
        }

        /// <inheritdoc/>
        public void Trigger()
        {
            if (State != ScenarioState.WaitingToTrigger) return;

            _triggerTime = Time.time;

            // Record speed at trigger
            Rigidbody rb = _vehicleTransform.GetComponent<Rigidbody>();
            if (rb != null)
                _speedAtTrigger = rb.velocity.magnitude;

            // Make hazard visible
            if (_hazardRoot != null)
                _hazardRoot.SetActive(true);

            State = ScenarioState.Active;
            Debug.Log($"[HazardScenario] {hazardType} hazard revealed!");
        }

        /// <inheritdoc/>
        public void UpdateScenario()
        {
            if (State != ScenarioState.Active) return;

            // ── Hazard-specific updates ──
            switch (hazardType)
            {
                case HazardType.AnimalCrossing:
                    UpdateAnimalCrossing();
                    break;
                case HazardType.StoppedVehicle:
                    UpdateBlinkingLight();
                    break;
            }

            // ── Metrics tracking ──
            Vector3 hazardPos = GetHazardCenterPosition();
            float dist = Vector3.Distance(_vehicleTransform.position, hazardPos);

            if (dist < _minDistance)
                _minDistance = dist;

            // Simplified brake detection
            if (!_brakeRecorded)
            {
                Rigidbody rb = _vehicleTransform.GetComponent<Rigidbody>();
                if (rb != null && rb.velocity.magnitude < _speedAtTrigger * 0.85f)
                {
                    _reactionTime = Time.time - _triggerTime;
                    _brakeRecorded = true;
                    _brakeStartPos = _vehicleTransform.position;
                    Debug.Log($"[HazardScenario] Brake detected. Reaction time: {_reactionTime:F2}s");
                }
            }
            else
            {
                // Track braking distance
                _brakingDistance = Vector3.Distance(_brakeStartPos, _vehicleTransform.position);
            }

            // ── Completion ──
            float vehicleZ = _vehicleTransform.position.z;
            bool pastHazard = vehicleZ > triggerPositionZ + 20f;
            bool animalDone = hazardType != HazardType.AnimalCrossing || _animalCrossed;

            if (pastHazard && animalDone)
            {
                CompleteScenario();
            }

            // Collision check
            if (dist < COLLISION_RADIUS)
            {
                Debug.LogWarning($"[HazardScenario] COLLISION with {hazardType}!");
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

        /// <summary>Reaction time in seconds. -1 if no brake detected.</summary>
        public float ReactionTime => _reactionTime;

        /// <summary>Minimum distance to the hazard (metres).</summary>
        public float MinimumDistance => _minDistance;

        /// <summary>Distance travelled while braking (metres).</summary>
        public float BrakingDistance => _brakingDistance;

        // ──────────────────────────── Hazard Creation ─────────────────────────────────

        private void CreateHazard()
        {
            _hazardRoot = new GameObject("HazardRoot");
            _hazardRoot.transform.SetParent(_scenarioContainer, false);

            switch (hazardType)
            {
                case HazardType.FallenTree:
                    CreateFallenTree();
                    break;
                case HazardType.StoppedVehicle:
                    CreateStoppedVehicle();
                    break;
                case HazardType.Debris:
                    CreateDebris();
                    break;
                case HazardType.AnimalCrossing:
                    CreateAnimalCrossing();
                    break;
            }
        }

        // ─── Fallen Tree ─────────────────────────────────────────────────────────

        private void CreateFallenTree()
        {
            // Horizontal cylinder (trunk) lying across the road
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "FallenTree_Trunk";
            trunk.transform.SetParent(_hazardRoot.transform, false);
            trunk.transform.position = new Vector3(0f, 0.4f, triggerPositionZ);
            trunk.transform.localScale = new Vector3(0.6f, roadWidth / 2f, 0.6f);
            trunk.transform.rotation = Quaternion.Euler(0f, 0f, 90f); // lay it across the road
            ApplyMaterial(trunk, new Color(0.45f, 0.28f, 0.1f)); // brown

            // Some foliage (sphere cluster)
            for (int i = 0; i < 3; i++)
            {
                GameObject foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                foliage.name = $"Foliage_{i}";
                foliage.transform.SetParent(_hazardRoot.transform, false);
                float xOff = Random.Range(-roadWidth / 3f, roadWidth / 3f);
                foliage.transform.position = new Vector3(xOff, 0.6f + Random.Range(0f, 0.3f), triggerPositionZ + Random.Range(-0.5f, 0.5f));
                foliage.transform.localScale = Vector3.one * Random.Range(0.8f, 1.5f);
                ApplyMaterial(foliage, new Color(0.15f, 0.45f, 0.12f)); // dark green
            }
        }

        // ─── Stopped Vehicle ─────────────────────────────────────────────────────

        private void CreateStoppedVehicle()
        {
            // Car body — box in the driving lane (right lane, positive X half)
            float laneCenter = laneWidth / 2f;

            GameObject carBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
            carBody.name = "StoppedVehicle_Body";
            carBody.transform.SetParent(_hazardRoot.transform, false);
            carBody.transform.position = new Vector3(laneCenter, 0.75f, triggerPositionZ);
            carBody.transform.localScale = new Vector3(1.8f, 1.5f, 4.5f);
            ApplyMaterial(carBody, new Color(0.7f, 0.15f, 0.1f)); // red

            // Roof
            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "StoppedVehicle_Roof";
            roof.transform.SetParent(_hazardRoot.transform, false);
            roof.transform.position = new Vector3(laneCenter, 1.6f, triggerPositionZ - 0.3f);
            roof.transform.localScale = new Vector3(1.6f, 0.6f, 2.5f);
            ApplyMaterial(roof, new Color(0.65f, 0.12f, 0.08f));

            // Blinking hazard light (small cube on rear)
            GameObject light = GameObject.CreatePrimitive(PrimitiveType.Cube);
            light.name = "HazardLight";
            light.transform.SetParent(_hazardRoot.transform, false);
            light.transform.position = new Vector3(laneCenter, 1.0f, triggerPositionZ - 2.3f);
            light.transform.localScale = new Vector3(0.3f, 0.15f, 0.05f);

            Material lightMat = new Material(Shader.Find("Standard"));
            lightMat.color = Color.yellow;
            lightMat.EnableKeyword("_EMISSION");
            lightMat.SetColor("_EmissionColor", Color.yellow * 2f);
            light.GetComponent<MeshRenderer>().material = lightMat;
            _blinkLight = light.GetComponent<MeshRenderer>();
        }

        // ─── Debris ──────────────────────────────────────────────────────────────

        private void CreateDebris()
        {
            int pieceCount = Random.Range(6, 12);

            for (int i = 0; i < pieceCount; i++)
            {
                GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = $"Debris_{i}";
                piece.transform.SetParent(_hazardRoot.transform, false);

                float x = Random.Range(-roadWidth / 3f, roadWidth / 3f);
                float z = triggerPositionZ + Random.Range(-3f, 3f);
                float size = Random.Range(0.15f, 0.6f);

                piece.transform.position = new Vector3(x, size / 2f, z);
                piece.transform.localScale = new Vector3(
                    size * Random.Range(0.5f, 1.5f),
                    size * Random.Range(0.5f, 1.0f),
                    size * Random.Range(0.5f, 1.5f));
                piece.transform.rotation = Quaternion.Euler(
                    Random.Range(0f, 30f),
                    Random.Range(0f, 360f),
                    Random.Range(0f, 30f));

                // Random debris colours: grays, browns, dark tones
                Color debrisColor = new Color(
                    Random.Range(0.2f, 0.5f),
                    Random.Range(0.2f, 0.4f),
                    Random.Range(0.15f, 0.35f));
                ApplyMaterial(piece, debrisColor);
            }
        }

        // ─── Animal Crossing ─────────────────────────────────────────────────────

        private void CreateAnimalCrossing()
        {
            float halfRoad = roadWidth / 2f;

            _animalStart = new Vector3(halfRoad + animalRunOffset, 0f, triggerPositionZ);
            _animalEnd = new Vector3(-(halfRoad + animalRunOffset), 0f, triggerPositionZ);

            _animal = new GameObject("Animal").transform;
            _animal.SetParent(_hazardRoot.transform, false);
            _animal.position = _animalStart;

            // Body (capsule — approximating a deer/dog shape)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "AnimalBody";
            body.transform.SetParent(_animal, false);
            body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            body.transform.localScale = new Vector3(0.3f, 0.4f, 0.3f);
            // Rotate capsule to be horizontal (animal facing crossing direction)
            body.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            ApplyMaterial(body, new Color(0.55f, 0.35f, 0.18f)); // brown

            // Head (sphere)
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "AnimalHead";
            head.transform.SetParent(_animal, false);
            head.transform.localPosition = new Vector3(-0.4f, 0.55f, 0f);
            head.transform.localScale = Vector3.one * 0.2f;
            ApplyMaterial(head, new Color(0.5f, 0.32f, 0.15f));

            // Legs (4 thin cylinders)
            float[] legXOffsets = { -0.15f, 0.15f, -0.15f, 0.15f };
            float[] legZOffsets = { 0.08f, 0.08f, -0.08f, -0.08f };
            for (int i = 0; i < 4; i++)
            {
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                leg.name = $"Leg_{i}";
                leg.transform.SetParent(_animal, false);
                leg.transform.localPosition = new Vector3(legXOffsets[i], 0.2f, legZOffsets[i]);
                leg.transform.localScale = new Vector3(0.06f, 0.2f, 0.06f);
                ApplyMaterial(leg, new Color(0.5f, 0.3f, 0.15f));
            }
        }

        // ──────────────────────────── Update Helpers ──────────────────────────────────

        private void UpdateAnimalCrossing()
        {
            if (_animal == null || _animalCrossed) return;

            _animal.position = Vector3.MoveTowards(
                _animal.position,
                _animalEnd,
                animalSpeed * Time.deltaTime);

            if (Vector3.Distance(_animal.position, _animalEnd) < 0.1f)
            {
                _animalCrossed = true;
                Debug.Log("[HazardScenario] Animal safely crossed.");
            }
        }

        private void UpdateBlinkingLight()
        {
            if (_blinkLight == null) return;

            _blinkTimer += Time.deltaTime;
            if (_blinkTimer >= blinkInterval)
            {
                _blinkTimer = 0f;
                _blinkOn = !_blinkOn;
                _blinkLight.enabled = _blinkOn;
            }
        }

        // ──────────────────────────── Utilities ───────────────────────────────────────

        private Vector3 GetHazardCenterPosition()
        {
            if (hazardType == HazardType.AnimalCrossing && _animal != null)
                return _animal.position;

            return new Vector3(0f, 0f, triggerPositionZ);
        }

        private void CompleteScenario()
        {
            if (State == ScenarioState.Completed) return;

            State = ScenarioState.Completed;
            Debug.Log($"[HazardScenario] {hazardType} scenario complete. " +
                      $"Reaction: {(_reactionTime >= 0 ? $"{_reactionTime:F2}s" : "N/A")}, " +
                      $"MinDist: {_minDistance:F2}m, BrakeDist: {_brakingDistance:F2}m");
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
            _hazardRoot = null;
            _animal = null;
            _blinkLight = null;
        }

        private static void ApplyMaterial(GameObject go, Color color)
        {
            Material mat = new Material(Shader.Find("Standard"))
            {
                color = color
            };
            mat.SetFloat("_Glossiness", 0.15f);
            mat.SetFloat("_Metallic", 0f);
            go.GetComponent<MeshRenderer>().material = mat;
        }
    }
}
