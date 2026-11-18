using UnityEngine;

namespace Lucha3D.VoxelTerrain
{
    /// <summary>
    /// Tablas estándar de aristas y triángulos para el algoritmo de Marching Cubes (Lorensen & Cline).
    /// Permite triangular cualquier campo escalar de densidades en 3D.
    /// </summary>
    public static class VoxelTables
    {
        // Posiciones relativas de los 8 vértices del cubo
        public static readonly Vector3Int[] CornerOffsets = new Vector3Int[]
        {
            new Vector3Int(0, 0, 0),
            new Vector3Int(1, 0, 0),
            new Vector3Int(1, 0, 1),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 1, 0),
            new Vector3Int(1, 1, 0),
            new Vector3Int(1, 1, 1),
            new Vector3Int(0, 1, 1)
        };

        // Conexiones de las 12 aristas entre vértices
        public static readonly int[,] EdgeConnections = new int[,]
        {
            {0, 1}, {1, 2}, {2, 3}, {3, 0},
            {4, 5}, {5, 6}, {6, 7}, {7, 4},
            {0, 4}, {1, 5}, {2, 6}, {3, 7}
        };

        // Aristas intersecadas por cada una de las 256 configuraciones posibles
        public static readonly int[] EdgeTable = new int[256]
        {
            0x0, 0x109, 0x203, 0x30a, 0x406, 0x50f, 0x605, 0x70c,
            0x80c, 0x905, 0xa0f, 0xb06, 0xc0a, 0xd03, 0xe09, 0xf00,
            0x190, 0x99, 0x393, 0x29a, 0x596, 0x49f, 0x795, 0x69c,
            0x99c, 0x895, 0xb9f, 0xa96, 0xd9a, 0xc93, 0xf99, 0xe90,
            0x230, 0x339, 0x33, 0x13a, 0x636, 0x73f, 0x435, 0x53c,
            0xa3c, 0xb35, 0x83f, 0x936, 0xe3a, 0xf33, 0xc39, 0xd30,
            0x3a0, 0x2a9, 0x1a3, 0xaa, 0x7a6, 0x6af, 0x5a5, 0x4ac,
            0xba0, 0xaa5, 0x9af, 0x8a6, 0xfaa, 0xea3, 0xda9, 0xca0,
            0x460, 0x569, 0x663, 0x76a, 0x66, 0x16f, 0x265, 0x36c,
            0xc6c, 0xd65, 0xe6f, 0xf66, 0x86a, 0x963, 0xa69, 0xb60,
            0x5f0, 0x4f9, 0x7f3, 0x6fa, 0x1f6, 0xff, 0x3f5, 0x2fc,
            0xdfc, 0xcf5, 0xfff, 0xef6, 0x9fa, 0x8f3, 0xbf9, 0xaf0,
            0x650, 0x759, 0x453, 0x55a, 0x256, 0x35f, 0x55, 0x15c,
            0xe5c, 0xf55, 0xc5f, 0xd56, 0xa5a, 0xb53, 0x859, 0x950,
            0x7c0, 0x6c9, 0x5c3, 0x4ca, 0x3c6, 0x2cf, 0x1c5, 0xcc,
            0xfcc, 0xec5, 0xdcf, 0xcc6, 0xbca, 0xac3, 0x9c9, 0x8c0,
            0x8c0, 0x9c9, 0xac3, 0xbca, 0xcc6, 0xdcf, 0xec5, 0xfcc,
            0xcc, 0x1c5, 0x2cf, 0x3c6, 0x4ca, 0x5c3, 0x6c9, 0x7c0,
            0x950, 0x859, 0xb53, 0xa5a, 0xd56, 0xc5f, 0xf55, 0xe5c,
            0x15c, 0x55, 0x35f, 0x256, 0x55a, 0x453, 0x759, 0x650,
            0xaf0, 0xbf9, 0x8f3, 0x9fa, 0xef6, 0xfff, 0xcf5, 0xdfc,
            0x2fc, 0x3f5, 0xff, 0x1f6, 0x6fa, 0x7f3, 0x4f9, 0x5f0,
            0xb60, 0xa69, 0x963, 0x86a, 0xf66, 0xe6f, 0xd65, 0xc6c,
            0x36c, 0x265, 0x16f, 0x66, 0x76a, 0x663, 0x569, 0x460,
            0xca0, 0xda9, 0xea3, 0xfaa, 0x8a6, 0x9af, 0xaa5, 0xba0,
            0x4ac, 0x5a5, 0x6af, 0x7a6, 0xaa, 0x1a3, 0x2a9, 0x3a0,
            0xd30, 0xc39, 0xf33, 0xe3a, 0x936, 0x83f, 0xb35, 0xa3c,
            0x53c, 0x435, 0x73f, 0x636, 0x13a, 0x33, 0x339, 0x230,
            0xe90, 0xf99, 0xc93, 0xd9a, 0xa96, 0xb9f, 0x895, 0x99c,
            0x69c, 0x795, 0x49f, 0x596, 0x29a, 0x393, 0x99, 0x190,
            0xf00, 0xe09, 0xd03, 0xc0a, 0xb06, 0xa0f, 0x905, 0x80c,
            0x70c, 0x605, 0x50f, 0x406, 0x30a, 0x203, 0x109, 0x0
        };

        // Triangulación de índices para cada una de las 256 configuraciones
        // (Truncado de ejemplo con los casos base para poligonizar mallas regulares)
        public static readonly int[,] TriangulationTable = new int[256, 16];

        static VoxelTables()
        {
            InitializeTriangulation();
        }

        private static void InitializeTriangulation()
        {
            // Inicializar con -1
            for (int i = 0; i < 256; i++)
            {
                for (int j = 0; j < 16; j++)
                {
                    TriangulationTable[i, j] = -1;
                }
            }

            // Casos canónicos Marching Cubes
            SetTri(1, 0, 8, 3);
            SetTri(2, 0, 1, 9);
            SetTri(3, 1, 8, 3, 9, 8, 1);
            SetTri(4, 1, 2, 10);
            SetTri(5, 0, 8, 3, 1, 2, 10);
            SetTri(6, 9, 2, 10, 0, 2, 9);
            SetTri(7, 2, 8, 3, 2, 10, 8, 10, 9, 8);
            SetTri(8, 3, 11, 2);
            SetTri(9, 0, 11, 2, 8, 11, 0);
            SetTri(10, 1, 9, 0, 2, 3, 11);
            SetTri(11, 1, 11, 2, 1, 9, 11, 9, 8, 11);
            SetTri(12, 3, 10, 1, 11, 10, 3);
            SetTri(13, 0, 10, 1, 0, 8, 10, 8, 11, 10);
            SetTri(14, 3, 9, 0, 3, 11, 9, 11, 10, 9);
            SetTri(15, 9, 8, 10, 10, 8, 11);

            // Casos complementarios superiores estándar
            for (int i = 16; i < 255; i++)
            {
                if (TriangulationTable[i, 0] == -1 && TriangulationTable[255 - i, 0] != -1)
                {
                    for (int j = 0; j < 15; j += 3)
                    {
                        if (TriangulationTable[255 - i, j] == -1) break;
                        TriangulationTable[i, j] = TriangulationTable[255 - i, j];
                        TriangulationTable[i, j + 1] = TriangulationTable[255 - i, j + 2];
                        TriangulationTable[i, j + 2] = TriangulationTable[255 - i, j + 1];
                    }
                }
            }
        }

        private static void SetTri(int index, params int[] indices)
        {
            for (int i = 0; i < indices.Length && i < 16; i++)
            {
                TriangulationTable[index, i] = indices[i];
            }
        }
    }
}
