// EnvironmentManager.cs — Orchestrates the full environment setup for AutoSim.
// Coordinates road generation, building placement, and cockpit creation in order.
using UnityEngine;

namespace AutoSim.Environment
{
    /// <summary>
    /// Central manager that coordinates all environment generators.
    /// Call <see cref="GenerateAll"/> to build the complete driving scene,
    /// or <see cref="ClearAll"/> to tear it down.
    /// </summary>
    [AddComponentMenu("AutoSim/Environment/Environment Manager")]
    public class EnvironmentManager : MonoBehaviour
    {
        // ──────────────────────────── Inspector References ────────────────────────────

        [Header("Generator References")]
        [Tooltip("Reference to the procedural road generator. Auto-found if null.")]
        [SerializeField] private ProceduralRoadGenerator roadGenerator;

        [Tooltip("Reference to the building generator. Auto-found if null.")]
        [SerializeField] private BuildingGenerator buildingGenerator;

        [Tooltip("Reference to the cockpit setup. Auto-found if null.")]
        [SerializeField] private CockpitSetup cockpitSetup;

        [Header("Startup")]
        [Tooltip("If true, the full environment is generated automatically in Awake.")]
        [SerializeField] private bool generateOnAwake = true;

        // ──────────────────────────── Unity Lifecycle ─────────────────────────────────

        private void Awake()
        {
            ResolveReferences();

            if (generateOnAwake)
            {
                GenerateAll();
            }
        }

        // ──────────────────────────── Public API ──────────────────────────────────────

        /// <summary>
        /// Generates the complete environment in the correct order:
        /// 1. Road → 2. Buildings → 3. Cockpit.
        /// </summary>
        public void GenerateAll()
        {
            ResolveReferences();

            Debug.Log("[EnvironmentManager] ── Starting environment generation ──");

            // Step 1: Road
            if (roadGenerator != null)
            {
                Debug.Log("[EnvironmentManager] Step 1/3 — Generating road…");
                roadGenerator.GenerateRoad();
            }
            else
            {
                Debug.LogWarning("[EnvironmentManager] ProceduralRoadGenerator not found. Skipping road.");
            }

            // Step 2: Buildings (depends on road dimensions)
            if (buildingGenerator != null && roadGenerator != null)
            {
                Debug.Log("[EnvironmentManager] Step 2/3 — Generating buildings…");
                buildingGenerator.GenerateBuildings(roadGenerator.RoadLength, roadGenerator.TotalRoadWidth);
            }
            else
            {
                Debug.LogWarning("[EnvironmentManager] BuildingGenerator or road data missing. Skipping buildings.");
            }

            // Step 3: Cockpit (skip if dedicated PolyDrive vehicle is present)
            if (cockpitSetup != null && FindObjectOfType<AutoSim.Vehicle.PolyDriveCarSetup>() == null)
            {
                Debug.Log("[EnvironmentManager] Step 3/3 — Generating cockpit…");
                cockpitSetup.GenerateCockpit();
            }
            else if (cockpitSetup != null)
            {
                cockpitSetup.ClearCockpit();
            }
            else
            {
                Debug.LogWarning("[EnvironmentManager] CockpitSetup not found. Skipping cockpit.");
            }

            Debug.Log("[EnvironmentManager] ── Environment generation complete ──");
        }

        /// <summary>
        /// Destroys all generated environment objects (road, buildings, cockpit).
        /// </summary>
        public void ClearAll()
        {
            Debug.Log("[EnvironmentManager] Clearing all generated environment objects…");

            if (cockpitSetup != null)
                cockpitSetup.ClearCockpit();

            if (buildingGenerator != null)
                buildingGenerator.ClearBuildings();

            if (roadGenerator != null)
                roadGenerator.ClearRoad();

            Debug.Log("[EnvironmentManager] Environment cleared.");
        }

        // ──────────────────────────── Helpers ─────────────────────────────────────────

        /// <summary>
        /// Attempts to auto-find any missing generator references on this GameObject
        /// or its children.
        /// </summary>
        private void ResolveReferences()
        {
            if (roadGenerator == null)
                roadGenerator = GetComponentInChildren<ProceduralRoadGenerator>();

            if (buildingGenerator == null)
                buildingGenerator = GetComponentInChildren<BuildingGenerator>();

            if (cockpitSetup == null)
                cockpitSetup = GetComponentInChildren<CockpitSetup>();
        }
    }
}
