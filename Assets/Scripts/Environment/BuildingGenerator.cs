// BuildingGenerator.cs — High-performance procedural urban city architecture.
// Spawns varied modern buildings (skyscrapers, commercial offices, apartments, storefronts)
// along the 2.5 km boulevard with shared batched materials for high FPS.
using System.Collections.Generic;
using UnityEngine;

namespace AutoSim.Environment
{
    /// <summary>
    /// Generates diverse modern urban buildings along both sides of the 2.5 km boulevard.
    /// Features varied architectural silhouettes (high-rise glass towers, mid-rise commercial,
    /// residential apartments, storefronts, and rooftop mechanicals).
    /// Uses cached shared materials for maximum rendering performance on PC and HoloLens 2.
    /// </summary>
    [AddComponentMenu("AutoSim/Environment/Building Generator")]
    public class BuildingGenerator : MonoBehaviour
    {
        // ──────────────────────────── Inspector Parameters ────────────────────────────

        [Header("Boulevard Placement")]
        [Tooltip("Distance from the road edge to the nearest building face (metres).")]
        [SerializeField] private float buildingSetback = 8f;

        [Tooltip("Minimum gap between adjacent buildings (metres).")]
        [SerializeField] private float minGap = 4f;

        [Tooltip("Maximum gap between adjacent buildings (metres).")]
        [SerializeField] private float maxGap = 12f;

        [Header("Building Dimensions")]
        [Tooltip("Length of building along the road (metres, min/max).")]
        [SerializeField] private Vector2 lengthRange = new Vector2(14f, 32f);

        [Tooltip("Depth of building away from the road (metres, min/max).")]
        [SerializeField] private Vector2 depthRange = new Vector2(16f, 30f);

        [Tooltip("Height of standard buildings (metres, min/max).")]
        [SerializeField] private Vector2 standardHeightRange = new Vector2(16f, 42f);

        [Tooltip("Height of landmark skyscrapers (metres, min/max).")]
        [SerializeField] private Vector2 skyscraperHeightRange = new Vector2(55f, 95f);

        [Tooltip("Probability of spawning a landmark skyscraper (0..1).")]
        [Range(0f, 0.4f)]
        [SerializeField] private float skyscraperChance = 0.22f;

        [Header("Architectural Features")]
        [Tooltip("Add rooftop HVAC structures and antennas.")]
        [SerializeField] private bool addRooftopDetails = true;

        [Tooltip("Add ground-floor storefront display bands.")]
        [SerializeField] private bool addGroundStorefronts = true;

        // ──────────────────────────── Internal Material Cache ─────────────────────────

        private Transform _buildingContainer;
        private List<Material> facadeMaterials = new List<Material>();
        private Material glassTowerMat;
        private Material storefrontMat;
        private Material roofTechMat;
        private bool materialsInit = false;

        // ──────────────────────────── Generation API ──────────────────────────────────

        /// <summary>
        /// Generates urban city blocks along the full boulevard length.
        /// </summary>
        /// <param name="roadLength">Total length of the boulevard (e.g. 2500m).</param>
        /// <param name="roadWidth">Total width including sidewalks (metres).</param>
        public void GenerateBuildings(float roadLength, float roadWidth)
        {
            ClearBuildings();
            InitMaterials();

            _buildingContainer = new GameObject("CityBuildings_2.5km").transform;
            _buildingContainer.SetParent(transform, false);

            // Left side city blocks
            GenerateBoulevardSide(roadLength, roadWidth, isLeftSide: true);

            // Right side city blocks
            GenerateBoulevardSide(roadLength, roadWidth, isLeftSide: false);

            Debug.Log($"[BuildingGenerator] Urban cityscape generated along {roadLength}m boulevard.");
        }

        /// <summary>Destroys all previously generated city buildings.</summary>
        public void ClearBuildings()
        {
            if (_buildingContainer != null)
            {
                if (Application.isPlaying)
                    Destroy(_buildingContainer.gameObject);
                else
                    DestroyImmediate(_buildingContainer.gameObject);
                _buildingContainer = null;
            }
        }

        // ──────────────────────────── Materials Setup ─────────────────────────────────

        private void InitMaterials()
        {
            if (materialsInit && facadeMaterials.Count > 0) return;

            facadeMaterials.Clear();

            // Modern architectural palette
            Color[] colors = new Color[]
            {
                new Color(0.24f, 0.26f, 0.30f), // Anthracite Charcoal
                new Color(0.82f, 0.84f, 0.86f), // White Concrete / Stucco
                new Color(0.42f, 0.46f, 0.52f), // Slate Steel
                new Color(0.76f, 0.70f, 0.60f), // Warm Travertine Stone
                new Color(0.30f, 0.36f, 0.44f), // Deep Cyan Office
                new Color(0.58f, 0.34f, 0.26f), // Architectural Terracotta Brick
                new Color(0.88f, 0.88f, 0.90f), // Platinum Modern Glass
                new Color(0.65f, 0.68f, 0.72f), // Silver Metallic
            };

            for (int i = 0; i < colors.Length; i++)
            {
                Material m = CreateMaterial($"Facade_{i}", colors[i], 0.25f, 0.1f);
                facadeMaterials.Add(m);
            }

            // Glass tower reflective cyan
            glassTowerMat = CreateMaterial("GlassTowerMat", new Color(0.20f, 0.45f, 0.60f), 0.85f, 0.35f);

            // Warm illuminated ground-floor storefront
            storefrontMat = CreateMaterial("StorefrontGlow", new Color(0.92f, 0.85f, 0.68f), 0.5f, 0.0f);

            // Roof mechanical equipment
            roofTechMat = CreateMaterial("RoofTechMat", new Color(0.20f, 0.22f, 0.24f), 0.1f, 0.2f);

            materialsInit = true;
        }

        private static Material CreateMaterial(string name, Color color, float glossiness, float metallic)
        {
            Material mat = new Material(Shader.Find("Standard"))
            {
                name = name,
                color = color
            };
            mat.SetFloat("_Glossiness", glossiness);
            mat.SetFloat("_Metallic", metallic);
            return mat;
        }

        // ──────────────────────────── Side Generation ─────────────────────────────────

        private void GenerateBoulevardSide(float roadLength, float roadWidth, bool isLeftSide)
        {
            string sideName = isLeftSide ? "West" : "East";
            Transform sideParent = new GameObject($"CityBlock_{sideName}").transform;
            sideParent.SetParent(_buildingContainer, false);

            float cursorZ = 20f; // Start with gentle setback from road start
            int buildingIdx = 0;

            while (cursorZ < roadLength - 40f)
            {
                // Randomise building parameters
                float bLength = Random.Range(lengthRange.x, lengthRange.y);
                float bDepth = Random.Range(depthRange.x, depthRange.y);

                if (cursorZ + bLength > roadLength - 20f) break;

                bool isSkyscraper = Random.value < skyscraperChance;
                float bHeight = isSkyscraper
                    ? Random.Range(skyscraperHeightRange.x, skyscraperHeightRange.y)
                    : Random.Range(standardHeightRange.x, standardHeightRange.y);

                // Setback from boulevard centerline
                float halfRoad = roadWidth / 2f;
                float xOffset = halfRoad + buildingSetback + (bDepth / 2f);
                float xPos = isLeftSide ? -xOffset : xOffset;
                float zPos = cursorZ + (bLength / 2f);
                float yPos = bHeight / 2f;

                // Pick facade material
                Material mat = isSkyscraper ? glassTowerMat : facadeMaterials[Random.Range(0, facadeMaterials.Count)];

                // Main Building Block
                GameObject buildingObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                buildingObj.name = $"Bld_{sideName}_{buildingIdx}_{(isSkyscraper ? "Tower" : "Block")}";
                buildingObj.transform.SetParent(sideParent, false);
                buildingObj.transform.position = new Vector3(xPos, yPos, zPos);
                buildingObj.transform.localScale = new Vector3(bDepth, bHeight, bLength);
                buildingObj.GetComponent<MeshRenderer>().sharedMaterial = mat;

                // Ground Floor Storefront Band
                if (addGroundStorefronts && bHeight > 12f)
                {
                    CreateStorefrontBand(buildingObj.transform, bDepth, bLength, isLeftSide);
                }

                // Rooftop Architectural Elements
                if (addRooftopDetails && bHeight > 20f)
                {
                    CreateRooftopFeatures(buildingObj.transform, bDepth, bHeight, bLength, isSkyscraper);
                }

                cursorZ += bLength + Random.Range(minGap, maxGap);
                buildingIdx++;
            }
        }

        // ──────────────────────────── Architectural Accents ───────────────────────────

        private void CreateStorefrontBand(Transform building, float depth, float length, bool isLeftSide)
        {
            float storeH = 3.6f;
            float storeThick = 0.35f;
            float localY = (-0.5f) + (storeH / 2f) / building.localScale.y;
            float localX = isLeftSide ? (0.5f + (storeThick / 2f) / depth) : (-0.5f - (storeThick / 2f) / depth);

            GameObject store = GameObject.CreatePrimitive(PrimitiveType.Cube);
            store.name = "Storefront";
            store.transform.SetParent(building, false);
            store.transform.localPosition = new Vector3(localX, localY, 0f);
            store.transform.localScale = new Vector3(storeThick / depth, storeH / building.localScale.y, 0.95f);
            DestroyImmediate(store.GetComponent<Collider>());
            store.GetComponent<MeshRenderer>().sharedMaterial = storefrontMat;
        }

        private void CreateRooftopFeatures(Transform building, float depth, float height, float length, bool isSkyscraper)
        {
            // Rooftop HVAC box
            GameObject hvac = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hvac.name = "RoofHVAC";
            hvac.transform.SetParent(building, false);
            hvac.transform.localPosition = new Vector3(0f, 0.5f + (1.2f / height), 0f);
            hvac.transform.localScale = new Vector3(0.45f, 2.4f / height, 0.45f);
            DestroyImmediate(hvac.GetComponent<Collider>());
            hvac.GetComponent<MeshRenderer>().sharedMaterial = roofTechMat;

            // Antenna on skyscrapers
            if (isSkyscraper)
            {
                float antennaH = 9.0f;
                GameObject antenna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                antenna.name = "Antenna";
                antenna.transform.SetParent(building, false);
                antenna.transform.localPosition = new Vector3(0f, 0.5f + (antennaH / 2f) / height, 0f);
                antenna.transform.localScale = new Vector3(0.04f, (antennaH / 2f) / height, 0.04f);
                DestroyImmediate(antenna.GetComponent<Collider>());
                antenna.GetComponent<MeshRenderer>().sharedMaterial = roofTechMat;
            }
        }
    }
}
