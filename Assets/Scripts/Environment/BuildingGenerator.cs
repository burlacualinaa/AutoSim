// BuildingGenerator.cs — Generates simple box buildings along both sides of the road.
using UnityEngine;

namespace AutoSim.Environment
{
    /// <summary>
    /// Spawns randomised box buildings on both sides of the road.
    /// Buildings have varied sizes, colours, and optional window patterns.
    /// All generated objects are parented under this transform for easy cleanup.
    /// </summary>
    [AddComponentMenu("AutoSim/Environment/Building Generator")]
    public class BuildingGenerator : MonoBehaviour
    {
        // ──────────────────────────── Inspector Parameters ────────────────────────────

        [Header("Building Placement")]
        [Tooltip("Distance from the road centre to the nearest building face (metres).")]
        [SerializeField] private float buildingSetback = 15f;

        [Tooltip("Minimum gap between adjacent buildings (metres).")]
        [SerializeField] private float minGap = 3f;

        [Tooltip("Maximum gap between adjacent buildings (metres).")]
        [SerializeField] private float maxGap = 8f;

        [Header("Building Size Ranges")]
        [Tooltip("Minimum / maximum building width along the road (X axis, metres).")]
        [SerializeField] private Vector2 widthRange = new Vector2(5f, 15f);

        [Tooltip("Minimum / maximum building height (Y axis, metres).")]
        [SerializeField] private Vector2 heightRange = new Vector2(8f, 30f);

        [Tooltip("Minimum / maximum building depth away from road (Z axis of building, metres).")]
        [SerializeField] private Vector2 depthRange = new Vector2(5f, 12f);

        [Header("Windows")]
        [Tooltip("Generate simple darker window sub-quads on building faces.")]
        [SerializeField] private bool generateWindows = true;

        [Tooltip("Approximate window size (metres).")]
        [SerializeField] private float windowSize = 1.2f;

        [Tooltip("Spacing between window centres (metres).")]
        [SerializeField] private float windowSpacing = 2.5f;

        // ──────────────────────────── Internals ───────────────────────────────────────

        /// <summary>Container for all generated building GameObjects.</summary>
        private Transform _buildingContainer;

        // Pre-defined building colour palette
        private static readonly Color[] BuildingColors = new Color[]
        {
            new Color(0.55f, 0.55f, 0.55f), // mid gray
            new Color(0.70f, 0.70f, 0.70f), // light gray
            new Color(0.40f, 0.40f, 0.45f), // dark gray-blue
            new Color(0.55f, 0.60f, 0.75f), // steel blue
            new Color(0.72f, 0.65f, 0.52f), // tan
            new Color(0.80f, 0.75f, 0.65f), // beige
            new Color(0.60f, 0.50f, 0.42f), // brown-tan
        };

        // ──────────────────────────── Public API ──────────────────────────────────────

        /// <summary>
        /// Generates buildings along both sides of the road.
        /// </summary>
        /// <param name="roadLength">Length of the road in metres.</param>
        /// <param name="roadWidth">Total width of the road (lanes + shoulders) in metres.</param>
        public void GenerateBuildings(float roadLength, float roadWidth)
        {
            ClearBuildings();

            _buildingContainer = new GameObject("Buildings").transform;
            _buildingContainer.SetParent(transform, false);

            // Generate buildings on the left side (negative X)
            GenerateSide(roadLength, roadWidth, isLeftSide: true);

            // Generate buildings on the right side (positive X)
            GenerateSide(roadLength, roadWidth, isLeftSide: false);

            Debug.Log("[BuildingGenerator] Buildings generated on both sides of the road.");
        }

        /// <summary>Destroys all previously generated buildings.</summary>
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

        // ──────────────────────────── Side Generation ─────────────────────────────────

        private void GenerateSide(float roadLength, float roadWidth, bool isLeftSide)
        {
            string sideName = isLeftSide ? "Left" : "Right";
            Transform sideParent = new GameObject($"Buildings_{sideName}").transform;
            sideParent.SetParent(_buildingContainer, false);

            float cursor = 0f; // Z position cursor
            int buildingIndex = 0;

            while (cursor < roadLength)
            {
                // Randomise building dimensions
                float bWidth = Random.Range(depthRange.x, depthRange.y);  // depth along X (away from road)
                float bHeight = Random.Range(heightRange.x, heightRange.y);
                float bLength = Random.Range(widthRange.x, widthRange.y); // length along Z (along road)

                // Check if building fits within road length
                if (cursor + bLength > roadLength) break;

                // Calculate X position: setback from road centre, plus half the building depth
                float xSign = isLeftSide ? -1f : 1f;
                float xPos = xSign * (roadWidth / 2f + buildingSetback + bWidth / 2f);
                float zPos = cursor + bLength / 2f;
                float yPos = bHeight / 2f;

                // Create the building
                GameObject building = CreateBoxBuilding(
                    $"Building_{sideName}_{buildingIndex}",
                    new Vector3(xPos, yPos, zPos),
                    new Vector3(bWidth, bHeight, bLength));
                building.transform.SetParent(sideParent, false);

                // Generate windows on the road-facing side
                if (generateWindows)
                {
                    GenerateWindowsOnFace(building.transform, bWidth, bHeight, bLength, isLeftSide);
                }

                // Advance cursor by building length + random gap
                cursor += bLength + Random.Range(minGap, maxGap);
                buildingIndex++;
            }
        }

        // ──────────────────────────── Building Creation ───────────────────────────────

        /// <summary>Creates a simple cube-based building with a random colour.</summary>
        private GameObject CreateBoxBuilding(string name, Vector3 position, Vector3 size)
        {
            GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.name = name;
            building.transform.position = position;
            building.transform.localScale = size;

            // Assign random building colour
            Color color = BuildingColors[Random.Range(0, BuildingColors.Length)];
            Material mat = new Material(Shader.Find("Standard"))
            {
                color = color
            };
            mat.SetFloat("_Glossiness", 0.1f);
            mat.SetFloat("_Metallic", 0f);
            building.GetComponent<MeshRenderer>().material = mat;

            return building;
        }

        // ──────────────────────────── Window Generation ───────────────────────────────

        /// <summary>
        /// Generates simple darker sub-quads on the road-facing face of the building
        /// to represent windows.
        /// </summary>
        private void GenerateWindowsOnFace(Transform building, float bWidth, float bHeight, float bLength,
            bool isLeftSide)
        {
            // Window colour: darker than the building
            Color buildingColor = building.GetComponent<MeshRenderer>().material.color;
            Color windowColor = buildingColor * 0.35f;
            windowColor.a = 1f;

            Material windowMat = new Material(Shader.Find("Standard"))
            {
                color = windowColor
            };
            windowMat.SetFloat("_Glossiness", 0.6f);
            windowMat.SetFloat("_Metallic", 0f);

            // Windows start 3m above ground, end 1m below roof
            float windowStartY = 3f;
            float windowEndY = bHeight - 1f;
            if (windowEndY <= windowStartY) return; // building too short for windows

            // Calculate number of window rows and columns
            int rows = Mathf.FloorToInt((windowEndY - windowStartY) / windowSpacing);
            int cols = Mathf.FloorToInt((bLength - 1f) / windowSpacing);
            if (rows <= 0 || cols <= 0) return;

            Transform windowContainer = new GameObject("Windows").transform;
            windowContainer.SetParent(building, false);

            // The road-facing face normal
            float xOffset = isLeftSide ? (bWidth / 2f + 0.01f) / bWidth : -(bWidth / 2f + 0.01f) / bWidth;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    // Position in local space of the building (scale = 1 in local)
                    float localY = (-0.5f + (windowStartY + r * windowSpacing + windowSpacing / 2f) / bHeight);
                    float localZ = (-0.5f + (0.5f + c * windowSpacing + windowSpacing / 2f) / bLength);

                    GameObject window = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    window.name = $"Window_{r}_{c}";

                    // Remove collider from windows — they're purely decorative
                    Object.Destroy(window.GetComponent<Collider>());

                    window.transform.SetParent(windowContainer, false);
                    window.transform.localPosition = new Vector3(xOffset, localY, localZ);
                    window.transform.localScale = new Vector3(
                        windowSize / bLength,   // scale relative to building
                        windowSize / bHeight,
                        1f);

                    // Rotate to face outward from the building
                    window.transform.localRotation = isLeftSide
                        ? Quaternion.Euler(0f, -90f, 0f)
                        : Quaternion.Euler(0f, 90f, 0f);

                    window.GetComponent<MeshRenderer>().material = windowMat;
                }
            }
        }
    }
}
