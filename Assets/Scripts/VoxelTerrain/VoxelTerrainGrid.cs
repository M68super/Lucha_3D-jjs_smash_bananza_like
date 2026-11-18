using System.Collections.Generic;
using UnityEngine;

namespace Lucha3D.VoxelTerrain
{
    /// <summary>
    /// Gestiona la cuadrícula de vóxeles 3D del suelo y genera la malla continua
    /// utilizando el algoritmo de Marching Cubes. Permite excavación y deformación
    /// en tiempo real por impactos o arranque de escombros, y reinicio íntegro de la arena.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    [DisallowMultipleComponent]
    public class VoxelTerrainGrid : MonoBehaviour
    {
        [Header("--- Dimensiones de la Cuadrícula Vóxel ---")]
        [Tooltip("Número de vóxeles en el eje X.")]
        [SerializeField] private int gridSizeX = 32;

        [Tooltip("Número de vóxeles en el eje Y (profundidad del suelo).")]
        [SerializeField] private int gridSizeY = 8;

        [Tooltip("Número de vóxeles en el eje Z.")]
        [SerializeField] private int gridSizeZ = 32;

        [Tooltip("Espaciado en metros entre vóxeles adyacentes.")]
        [SerializeField] private float voxelScale = 1.0f;

        [Tooltip("Nivel isométrico (umbral para considerar sólida una zona).")]
        [Range(0f, 1f)]
        [SerializeField] private float isoLevel = 0.5f;

        [Tooltip("Altura base de la superficie sólida inicial.")]
        [SerializeField] private int baseSurfaceLevel = 4;

        private float[,,] _densityField;
        private float[,,] _initialDensityBackup;

        private MeshFilter _meshFilter;
        private MeshCollider _meshCollider;
        private Mesh _terrainMesh;

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<int> _triangles = new List<int>();

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshCollider = GetComponent<MeshCollider>();

            _terrainMesh = new Mesh { name = "VoxelTerrain_Mesh" };
            _meshFilter.sharedMesh = _terrainMesh;

            InitializeDensities();
            GenerateMesh();
        }

        #region Inicialización y Backup para Reinicio
        private void InitializeDensities()
        {
            _densityField = new float[gridSizeX, gridSizeY, gridSizeZ];
            _initialDensityBackup = new float[gridSizeX, gridSizeY, gridSizeZ];

            for (int x = 0; x < gridSizeX; x++)
            {
                for (int y = 0; y < gridSizeY; y++)
                {
                    for (int z = 0; z < gridSizeZ; z++)
                    {
                        // Densidad: 1.0 sólido en niveles inferiores, 0.0 aire en niveles superiores
                        float density = y < baseSurfaceLevel ? 1.0f : (y == baseSurfaceLevel ? 0.6f : 0.0f);
                        _densityField[x, y, z] = density;
                        _initialDensityBackup[x, y, z] = density;
                    }
                }
            }
        }

        /// <summary>
        /// Restaura el terreno a su estado original (llamado por el gestor de reinicio de 5 minutos).
        /// </summary>
        public void ResetTerrain()
        {
            for (int x = 0; x < gridSizeX; x++)
            {
                for (int y = 0; y < gridSizeY; y++)
                {
                    for (int z = 0; z < gridSizeZ; z++)
                    {
                        _densityField[x, y, z] = _initialDensityBackup[x, y, z];
                    }
                }
            }

            GenerateMesh();
        }
        #endregion

        #region Deformación y Excavación en Tiempo Real
        /// <summary>
        /// Sustrae densidad en un punto esférico (crea un cráter o arranca escombros).
        /// </summary>
        public void CarveSphere(Vector3 worldPoint, float radius, float strength = 1.0f)
        {
            Vector3 localPoint = transform.InverseTransformPoint(worldPoint) / voxelScale;
            int minX = Mathf.Max(0, Mathf.FloorToInt(localPoint.x - radius));
            int maxX = Mathf.Min(gridSizeX - 1, Mathf.CeilToInt(localPoint.x + radius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(localPoint.y - radius));
            int maxY = Mathf.Min(gridSizeY - 1, Mathf.CeilToInt(localPoint.y + radius));
            int minZ = Mathf.Max(0, Mathf.FloorToInt(localPoint.z - radius));
            int maxZ = Mathf.Min(gridSizeZ - 1, Mathf.CeilToInt(localPoint.z + radius));

            bool modified = false;

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        float dist = Vector3.Distance(new Vector3(x, y, z), localPoint);
                        if (dist <= radius)
                        {
                            float falloff = 1.0f - (dist / radius);
                            _densityField[x, y, z] = Mathf.Max(0f, _densityField[x, y, z] - falloff * strength);
                            modified = true;
                        }
                    }
                }
            }

            if (modified)
            {
                GenerateMesh();
            }
        }
        #endregion

        #region Generación de Malla Marching Cubes
        public void GenerateMesh()
        {
            _vertices.Clear();
            _triangles.Clear();

            for (int x = 0; x < gridSizeX - 1; x++)
            {
                for (int y = 0; y < gridSizeY - 1; y++)
                {
                    for (int z = 0; z < gridSizeZ - 1; z++)
                    {
                        PolygonizeCell(x, y, z);
                    }
                }
            }

            _terrainMesh.Clear();
            _terrainMesh.SetVertices(_vertices);
            _terrainMesh.SetTriangles(_triangles, 0);
            _terrainMesh.RecalculateNormals();
            _terrainMesh.RecalculateBounds();

            // Actualizar colisionador físico
            _meshCollider.sharedMesh = null;
            _meshCollider.sharedMesh = _terrainMesh;
        }

        private void PolygonizeCell(int x, int y, int z)
        {
            float[] cubeValues = new float[8];
            Vector3[] cornerPositions = new Vector3[8];

            for (int i = 0; i < 8; i++)
            {
                Vector3Int offset = VoxelTables.CornerOffsets[i];
                cubeValues[i] = _densityField[x + offset.x, y + offset.y, z + offset.z];
                cornerPositions[i] = new Vector3((x + offset.x) * voxelScale, (y + offset.y) * voxelScale, (z + offset.z) * voxelScale);
            }

            // Calcular índice de configuración (0 a 255)
            int cubeIndex = 0;
            for (int i = 0; i < 8; i++)
            {
                if (cubeValues[i] > isoLevel)
                {
                    cubeIndex |= 1 << i;
                }
            }

            int edgeFlag = VoxelTables.EdgeTable[cubeIndex];
            if (edgeFlag == 0) return; // Fuera o dentro por completo: sin polígonos

            Vector3[] edgeVertices = new Vector3[12];
            for (int i = 0; i < 12; i++)
            {
                if ((edgeFlag & (1 << i)) != 0)
                {
                    int c1 = VoxelTables.EdgeConnections[i, 0];
                    int c2 = VoxelTables.EdgeConnections[i, 1];
                    edgeVertices[i] = InterpolateVertex(cornerPositions[c1], cornerPositions[c2], cubeValues[c1], cubeValues[c2]);
                }
            }

            // Añadir triángulos
            for (int i = 0; i < 16; i += 3)
            {
                int e0 = VoxelTables.TriangulationTable[cubeIndex, i];
                if (e0 == -1) break;

                int e1 = VoxelTables.TriangulationTable[cubeIndex, i + 1];
                int e2 = VoxelTables.TriangulationTable[cubeIndex, i + 2];

                int baseIndex = _vertices.Count;
                _vertices.Add(edgeVertices[e0]);
                _vertices.Add(edgeVertices[e1]);
                _vertices.Add(edgeVertices[e2]);

                _triangles.Add(baseIndex);
                _triangles.Add(baseIndex + 1);
                _triangles.Add(baseIndex + 2);
            }
        }

        private Vector3 InterpolateVertex(Vector3 p1, Vector3 p2, float v1, float v2)
        {
            if (Mathf.Abs(isoLevel - v1) < 0.00001f) return p1;
            if (Mathf.Abs(isoLevel - v2) < 0.00001f) return p2;
            if (Mathf.Abs(v1 - v2) < 0.00001f) return p1;

            float t = (isoLevel - v1) / (v2 - v1);
            return Vector3.Lerp(p1, p2, t);
        }
        #endregion
    }
}
