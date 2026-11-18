#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Lucha3D.Movement;
using Lucha3D.Combat;
using Lucha3D.Abilities;
using Lucha3D.VoxelTerrain;
using Lucha3D.Destruction;
using Lucha3D.Core;
using Lucha3D.UI;

namespace Lucha3D.Editor
{
    /// <summary>
    /// Herramienta de Editor para construir y guardar automáticamente la escena completa de prueba
    /// con personajes, maniquíes, terreno vóxel, muro destructible, cámara y UI.
    /// Accesible desde el menú superior: Lucha3D -> Construir Escena de Prueba.
    /// </summary>
    public static class ArenaSceneBuilder
    {
        private const string SCENE_PATH = "Assets/Scenes/CombatTestScene.unity";
        private const string MATERIALS_DIR = "Assets/Materials";

        [MenuItem("Lucha3D/Construir Escena de Prueba (CombatTestScene)")]
        public static void BuildSceneMenuItem()
        {
            BuildCombatTestScene();
        }

        public static void BuildSceneFromCommandLine()
        {
            Debug.Log("[ArenaSceneBuilder] Iniciando generación de escena por línea de comandos...");
            BuildCombatTestScene();
            EditorApplication.Exit(0);
        }

        public static void BuildCombatTestScene()
        {
            // 1. Asegurar directorios
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!AssetDatabase.IsValidFolder(MATERIALS_DIR)) AssetDatabase.CreateFolder("Assets", "Materials");

            // 2. Crear nueva escena vacía
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 3. Crear o cargar materiales Cel-Shading
            var celShader = Shader.Find("Lucha3D/AnimeCelShading");
            if (celShader == null) celShader = Shader.Find("Universal Render Pipeline/Lit");

            var playerMat = GetOrCreateMaterial($"{MATERIALS_DIR}/Mat_Player_Anime.mat", celShader, new Color(0.2f, 0.85f, 0.95f), new Color(0.1f, 0.25f, 0.45f));
            var dummyAMat = GetOrCreateMaterial($"{MATERIALS_DIR}/Mat_DummyA_Anime.mat", celShader, new Color(0.95f, 0.25f, 0.35f), new Color(0.4f, 0.08f, 0.15f));
            var dummyBMat = GetOrCreateMaterial($"{MATERIALS_DIR}/Mat_DummyB_Anime.mat", celShader, new Color(0.7f, 0.3f, 0.95f), new Color(0.25f, 0.05f, 0.4f));
            var groundMat = GetOrCreateMaterial($"{MATERIALS_DIR}/Mat_Ground_Anime.mat", celShader, new Color(0.35f, 0.65f, 0.45f), new Color(0.15f, 0.3f, 0.2f));
            var wallMat = GetOrCreateMaterial($"{MATERIALS_DIR}/Mat_Wall_Anime.mat", celShader, new Color(0.85f, 0.75f, 0.65f), new Color(0.45f, 0.38f, 0.32f));

            // 4. Iluminación Anime
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.98f, 0.92f);
            light.intensity = 1.25f;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 5. Terreno Vóxel (Marching Cubes)
            var terrainObj = new GameObject("VoxelTerrain_Ground");
            terrainObj.transform.position = new Vector3(-16f, 0f, -16f);
            var meshFilter = terrainObj.AddComponent<MeshFilter>();
            var meshRenderer = terrainObj.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = groundMat;
            var meshCollider = terrainObj.AddComponent<MeshCollider>();
            var voxelGrid = terrainObj.AddComponent<VoxelTerrainGrid>();

            // Suelo plano de soporte físico
            var planeObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
            planeObj.name = "Floor_SafetyPlane";
            planeObj.transform.position = new Vector3(0f, 3.95f, 0f);
            planeObj.transform.localScale = new Vector3(5f, 1f, 5f);
            planeObj.GetComponent<Renderer>().sharedMaterial = groundMat;

            // 6. Personaje Luchador (Player)
            var playerObj = new GameObject("Player_Luchador");
            playerObj.transform.position = new Vector3(0f, 4.5f, -2f);

            var mannequin = CreateMannequinVisual("Visual_Mannequin", playerMat, isPlayer: true);
            mannequin.transform.SetParent(playerObj.transform, false);

            var handSocket = new GameObject("HandSocket_Debris");
            handSocket.transform.SetParent(playerObj.transform);
            handSocket.transform.localPosition = new Vector3(0.5f, 1.1f, 0.7f);

            var cc = playerObj.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.45f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            var inputReader = playerObj.AddComponent<PlayerInputReader>();
            var health = playerObj.AddComponent<HealthSystem>();
            var staminaSystem = playerObj.AddComponent<StaminaSystem>();
            var locomotion = playerObj.AddComponent<PlayerLocomotion>();
            var combat = playerObj.AddComponent<PlayerCombatController>();
            var boogie = playerObj.AddComponent<BoogieWoogieAbility>();
            var projection = playerObj.AddComponent<ProjectionTechnique>();
            var debrisHandler = playerObj.AddComponent<PlayerDebrisHandler>();
            var loadout = playerObj.AddComponent<AbilityLoadoutManager>();
            var playerTargetable = playerObj.AddComponent<TargetableEntity>();

            // 7. Maniquíes de Entrenamiento (Dummy A y Dummy B)
            var dummyA = CreateDummy("Dummy_A_Carmesí", new Vector3(-3f, 4.5f, 6f), dummyAMat);
            var dummyB = CreateDummy("Dummy_B_Púrpura", new Vector3(4f, 4.5f, 5f), dummyBMat);

            // 8. Muro Destructible Modular
            var wallObj = CreateModularDestructibleWall("Destructible_ModularWall", new Vector3(-6f, 4.0f, 3f), wallMat);

            // 9. Cámara en Tercera Persona
            var camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            var cam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            var camFollow = camObj.AddComponent<CameraFollow>();
            camFollow.Target = playerObj.transform;

            // 10. Gestor de Reinicio de 5 minutos
            var resetManagerObj = new GameObject("ArenaReset_Manager");
            var resetManager = resetManagerObj.AddComponent<ArenaResetManager>();

            // 11. HUD Canvas
            var canvasObj = new GameObject("HUD_Canvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            canvasObj.AddComponent<PlayerHUD>();

            // 12. Guardar escena
            EditorSceneManager.SaveScene(newScene, SCENE_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ArenaSceneBuilder] ¡Escena de prueba creada con éxito en {SCENE_PATH}!");
        }

        private static Material GetOrCreateMaterial(string path, Shader shader, Color baseColor, Color shadowColor)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
                if (mat.HasProperty("_ShadowColor")) mat.SetColor("_ShadowColor", shadowColor);
                if (mat.HasProperty("_OutlineWidth")) mat.SetFloat("_OutlineWidth", 0.006f);
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static GameObject CreateMannequinVisual(string name, Material mat, bool isPlayer)
        {
            var root = new GameObject(name);

            // Torso
            var torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
            torso.name = "Torso";
            torso.transform.SetParent(root.transform);
            torso.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            torso.transform.localScale = new Vector3(0.65f, 0.75f, 0.35f);
            Object.DestroyImmediate(torso.GetComponent<Collider>());
            torso.GetComponent<Renderer>().sharedMaterial = mat;

            // Cabeza
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform);
            head.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            head.transform.localScale = new Vector3(0.4f, 0.45f, 0.4f);
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().sharedMaterial = mat;

            // Brazos
            var leftArm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftArm.name = "LeftArm";
            leftArm.transform.SetParent(root.transform);
            leftArm.transform.localPosition = new Vector3(-0.45f, 1.05f, 0f);
            leftArm.transform.localScale = new Vector3(0.2f, 0.7f, 0.2f);
            Object.DestroyImmediate(leftArm.GetComponent<Collider>());
            leftArm.GetComponent<Renderer>().sharedMaterial = mat;

            var rightArm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightArm.name = "RightArm";
            rightArm.transform.SetParent(root.transform);
            rightArm.transform.localPosition = new Vector3(0.45f, 1.05f, 0f);
            rightArm.transform.localScale = new Vector3(0.2f, 0.7f, 0.2f);
            Object.DestroyImmediate(rightArm.GetComponent<Collider>());
            rightArm.GetComponent<Renderer>().sharedMaterial = mat;

            // Piernas
            var leftLeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftLeg.name = "LeftLeg";
            leftLeg.transform.SetParent(root.transform);
            leftLeg.transform.localPosition = new Vector3(-0.2f, 0.35f, 0f);
            leftLeg.transform.localScale = new Vector3(0.22f, 0.7f, 0.25f);
            Object.DestroyImmediate(leftLeg.GetComponent<Collider>());
            leftLeg.GetComponent<Renderer>().sharedMaterial = mat;

            var rightLeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightLeg.name = "RightLeg";
            rightLeg.transform.SetParent(root.transform);
            rightLeg.transform.localPosition = new Vector3(0.2f, 0.35f, 0f);
            rightLeg.transform.localScale = new Vector3(0.22f, 0.7f, 0.25f);
            Object.DestroyImmediate(rightLeg.GetComponent<Collider>());
            rightLeg.GetComponent<Renderer>().sharedMaterial = mat;

            return root;
        }

        private static GameObject CreateDummy(string name, Vector3 position, Material mat)
        {
            var dummyObj = new GameObject(name);
            dummyObj.transform.position = position;

            var mannequin = CreateMannequinVisual("Visual_Dummy", mat, isPlayer: false);
            mannequin.transform.SetParent(dummyObj.transform, false);

            var cc = dummyObj.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.45f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            // Los maniquíes son estáticos: NO llevan PlayerInputReader ni PlayerLocomotion
            dummyObj.AddComponent<TargetableEntity>();
            dummyObj.AddComponent<HealthSystem>(); // 200 HP, 4s delay para regenerar, desaparece al morir
            dummyObj.AddComponent<TrainingDummy>(); // Gestiona impactos, knockback amortiguado y daño

            return dummyObj;
        }

        private static GameObject CreateModularDestructibleWall(string name, Vector3 position, Material mat)
        {
            var wallObj = new GameObject(name);
            wallObj.transform.position = position;

            // 1. Intacto
            var intactRoot = new GameObject("Intact_Visual");
            intactRoot.transform.SetParent(wallObj.transform, false);
            var intactMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            intactMesh.name = "Intact_Mesh";
            intactMesh.transform.SetParent(intactRoot.transform, false);
            intactMesh.transform.localScale = new Vector3(4f, 3f, 0.6f);
            intactMesh.GetComponent<Renderer>().sharedMaterial = mat;

            // 2. Fracturado
            var fracturedRoot = new GameObject("Fractured_Pieces");
            fracturedRoot.transform.SetParent(wallObj.transform, false);

            int cols = 4;
            int rows = 3;
            float w = 4f / cols;
            float h = 3f / rows;

            for (int x = 0; x < cols; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    piece.name = $"Piece_{x}_{y}";
                    piece.transform.SetParent(fracturedRoot.transform, false);
                    float posX = (x - (cols - 1) * 0.5f) * w;
                    float posY = (y - (rows - 1) * 0.5f) * h;
                    piece.transform.localPosition = new Vector3(posX, posY, 0f);
                    piece.transform.localScale = new Vector3(w * 0.95f, h * 0.95f, 0.55f);
                    piece.GetComponent<Renderer>().sharedMaterial = mat;

                    var rb = piece.AddComponent<Rigidbody>();
                    rb.mass = 15f;
                    rb.isKinematic = true;

                    piece.AddComponent<TargetableEntity>();
                }
            }
            fracturedRoot.SetActive(false);

            wallObj.AddComponent<DestructibleStructure>();
            return wallObj;
        }
    }
}
#endif
