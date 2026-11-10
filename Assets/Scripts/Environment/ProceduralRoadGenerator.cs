// ProceduralRoadGenerator.cs — Generates a procedural 2-lane road network at runtime.
// Creates road surface, lane markings, edge lines, and a surrounding ground plane.
using UnityEngine;

namespace AutoSim.Environment
{
    /// <summary>
    /// Generates a straight 2-lane road with dashed center line, solid edge lines,
    /// shoulders, and a green ground plane. All geometry is procedural — no prefabs needed.
    /// </summary>
    [AddComponentMenu("AutoSim/Environment/Procedural Road Generator")]
    public class ProceduralRoadGenerator : MonoBehaviour
    {
        // ──────────────────────────── Inspector Parameters ────────────────────────────

        [Header("Road Dimensions")]
        [Tooltip("Total length of the road along the Z axis (metres).")]
        [SerializeField] private float roadLength = 500f;

        [Tooltip("Width of a single lane (metres). Total road = 2 × laneWidth.")]
        [SerializeField] private float laneWidth = 3.5f;

        [Tooltip("Width of the shoulder on each side of the road (metres).")]
        [SerializeField] private float shoulderWidth = 0.5f;

        [Header("Lane Marking Dimensions")]
        [Tooltip("Length of each center-line dash (metres).")]
        [SerializeField] private float dashLength = 3f;

        [Tooltip("Gap between center-line dashes (metres).")]
        [SerializeField] private float dashGap = 6f;

        [Tooltip("Width of lane markings (metres).")]
        [SerializeField] private float markingWidth = 0.15f;

        [Tooltip("Height offset for markings above road to avoid Z-fighting (metres).")]
        [SerializeField] private float markingYOffset = 0.01f;

        [Header("Ground Plane")]
        [Tooltip("Width of the ground plane on each side of the road (metres).")]
        [SerializeField] private float groundExtent = 60f;

        [Header("Runtime Options")]
        [Tooltip("Automatically generate the road on Start if not already generated.")]
        [SerializeField] private bool generateOnStart = true;

        // ──────────────────────────── Cached References ───────────────────────────────

        /// <summary>Total road width including both lanes and shoulders.</summary>
        public float TotalRoadWidth => (laneWidth * 2f) + (shoulderWidth * 2f);

        /// <summary>Total road length.</summary>
        public float RoadLength => roadLength;

        // Container transforms for easy cleanup
        private Transform _roadContainer;

        // ──────────────────────────── Public API ──────────────────────────────────────

        private void Start()
        {
            if (generateOnStart && _roadContainer == null)
            {
                GenerateRoad();
            }
        }

        /// <summary>
        /// Generates the entire road network. Can be called from Awake, editor tools,
        /// or <see cref="EnvironmentManager"/>.
        /// </summary>
        public void GenerateRoad()
        {
            // Clean up any previous generation
            ClearRoad();

            // Create a container for all road objects
            _roadContainer = new GameObject("RoadNetwork").transform;
            _roadContainer.SetParent(transform, false);

            CreateGroundPlane();
            CreateRoadSurface();
            CreateCenterLineDashes();
            CreateEdgeLines();

            Debug.Log($"[ProceduralRoadGenerator] Road generated: {roadLength}m long, " +
                      $"{TotalRoadWidth}m wide ({laneWidth}m lanes + {shoulderWidth}m shoulders).");
        }

        /// <summary>Destroys all previously generated road geometry.</summary>
        public void ClearRoad()
        {
            if (_roadContainer != null)
            {
                if (Application.isPlaying)
                    Destroy(_roadContainer.gameObject);
                else
                    DestroyImmediate(_roadContainer.gameObject);
                _roadContainer = null;
            }
        }

        // ──────────────────────────── Ground Plane ────────────────────────────────────

        private void CreateGroundPlane()
        {
            float totalWidth = TotalRoadWidth + groundExtent * 2f;

            // Left ground (negative X)
            CreateQuadObject("Ground_Left",
                new Vector3(-(TotalRoadWidth / 2f + groundExtent / 2f), -0.01f, roadLength / 2f),
                groundExtent, roadLength,
                CreateFlatMaterial("GroundMat", new Color(0.2f, 0.55f, 0.15f))); // green

            // Right ground (positive X)
            CreateQuadObject("Ground_Right",
                new Vector3(TotalRoadWidth / 2f + groundExtent / 2f, -0.01f, roadLength / 2f),
                groundExtent, roadLength,
                CreateFlatMaterial("GroundMat_R", new Color(0.2f, 0.55f, 0.15f)));
        }

        // ──────────────────────────── Road Surface ────────────────────────────────────

        private void CreateRoadSurface()
        {
            CreateQuadObject("RoadSurface",
                new Vector3(0f, 0f, roadLength / 2f),
                TotalRoadWidth, roadLength,
                CreateFlatMaterial("RoadMat", new Color(0.18f, 0.18f, 0.2f))); // dark gray asphalt
        }

        // ──────────────────────────── Center Line Dashes ──────────────────────────────

        private void CreateCenterLineDashes()
        {
            Material dashMat = CreateFlatMaterial("DashMat", Color.white);
            float cycle = dashLength + dashGap;
            int dashCount = Mathf.FloorToInt(roadLength / cycle);

            // Container for dashes
            Transform dashContainer = new GameObject("CenterLineDashes").transform;
            dashContainer.SetParent(_roadContainer, false);

            for (int i = 0; i < dashCount; i++)
            {
                float zStart = i * cycle;
                float zCenter = zStart + dashLength / 2f;

                GameObject dash = CreateQuadMesh(
                    new Vector3(0f, markingYOffset, zCenter),
                    markingWidth, dashLength);
                dash.name = $"Dash_{i}";
                dash.GetComponent<MeshRenderer>().sharedMaterial = dashMat;
                dash.transform.SetParent(dashContainer, false);
            }
        }

        // ──────────────────────────── Edge Lines ──────────────────────────────────────

        private void CreateEdgeLines()
        {
            Material edgeMat = CreateFlatMaterial("EdgeLineMat", Color.white);

            float halfRoad = TotalRoadWidth / 2f;

            // Left edge line (inner edge of left shoulder)
            GameObject leftEdge = CreateQuadMesh(
                new Vector3(-halfRoad + shoulderWidth, markingYOffset, roadLength / 2f),
                markingWidth, roadLength);
            leftEdge.name = "EdgeLine_Left";
            leftEdge.GetComponent<MeshRenderer>().sharedMaterial = edgeMat;
            leftEdge.transform.SetParent(_roadContainer, false);

            // Right edge line (inner edge of right shoulder)
            GameObject rightEdge = CreateQuadMesh(
                new Vector3(halfRoad - shoulderWidth, markingYOffset, roadLength / 2f),
                markingWidth, roadLength);
            rightEdge.name = "EdgeLine_Right";
            rightEdge.GetComponent<MeshRenderer>().sharedMaterial = edgeMat;
            rightEdge.transform.SetParent(_roadContainer, false);
        }

        // ──────────────────────────── Mesh Helpers ────────────────────────────────────

        /// <summary>
        /// Creates a flat horizontal quad mesh centred at <paramref name="origin"/>
        /// with the given <paramref name="width"/> (X) and <paramref name="length"/> (Z).
        /// </summary>
        /// <returns>A new GameObject with MeshFilter and MeshRenderer.</returns>
        public static GameObject CreateQuadMesh(Vector3 origin, float width, float length)
        {
            GameObject go = new GameObject("Quad");

            float hw = width / 2f;
            float hl = length / 2f;

            Vector3[] verts = new Vector3[]
            {
                new Vector3(-hw, 0f, -hl),
                new Vector3( hw, 0f, -hl),
                new Vector3( hw, 0f,  hl),
                new Vector3(-hw, 0f,  hl)
            };

            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };

            int[] tris = new int[] { 0, 2, 1, 0, 3, 2 };

            Vector3[] normals = new Vector3[]
            {
                Vector3.up, Vector3.up, Vector3.up, Vector3.up
            };

            Mesh mesh = new Mesh
            {
                name = "QuadMesh",
                vertices = verts,
                uv = uvs,
                triangles = tris,
                normals = normals
            };

            go.AddComponent<MeshFilter>().mesh = mesh;
            go.AddComponent<MeshRenderer>();
            go.transform.position = origin;

            return go;
        }

        /// <summary>
        /// Convenience wrapper that creates a named quad and parents it under <see cref="_roadContainer"/>.
        /// </summary>
        private void CreateQuadObject(string name, Vector3 position, float width, float length, Material mat, bool addCollider = true)
        {
            GameObject quad = CreateQuadMesh(position, width, length);
            quad.name = name;
            quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (addCollider)
            {
                var col = quad.AddComponent<MeshCollider>();
                col.sharedMesh = quad.GetComponent<MeshFilter>().sharedMesh;
            }
            quad.transform.SetParent(_roadContainer, true);
        }

        /// <summary>Creates a simple unlit‐color material at runtime.</summary>
        private static Material CreateFlatMaterial(string name, Color color)
        {
            // Use the built-in Standard shader; works on UWP / HoloLens 2
            Material mat = new Material(Shader.Find("Standard"))
            {
                name = name,
                color = color
            };
            // Disable specular highlights for flat appearance
            mat.SetFloat("_Glossiness", 0f);
            mat.SetFloat("_Metallic", 0f);
            return mat;
        }
    }
}
