using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Lucha3D.Movement;
using Lucha3D.Combat;
using Lucha3D.Abilities;
using Lucha3D.VoxelTerrain;
using Lucha3D.Destruction;
using Lucha3D.UI;

namespace Lucha3D.Core
{
    /// <summary>
    /// Generador automático de escenario y personajes de prueba en tiempo de ejecución.
    /// Si se coloca en cualquier escena vacía, ensambla proceduralmente:
    /// 1. Iluminación y materiales Cel-Shading Anime.
    /// 2. Suelo con cuadrícula vóxel de Marching Cubes y arena de combate.
    /// 3. Modelo básico estilizado de Luchador (Jugador) con todos los scripts integrados.
    /// 4. Dos maniquíes de combate independientes para probar Boogie-Woogie y la Técnica de Proyección.
    /// 5. Estructura modular destructible (muro rompible en fragmentos físicos).
    /// 6. Cámara en 3ª persona orbital y HUD visual reactivo.
    /// 7. Gestor de reinicio de 5 minutos.
    /// </summary>
    [DisallowMultipleComponent]
    public class TestArenaBootstrap : MonoBehaviour
    {
        [Header("--- Shader Anime Cel-Shading ---")]
        [SerializeField] private Shader celShadingShader;

        private Material _playerMat;
        private Material _enemyMatA;
        private Material _enemyMatB;
        private Material _groundMat;
        private Material _wallMat;
        private Material _debrisMat;

        private GameObject _playerObj;
        private Camera _mainCamera;
        private ArenaResetManager _resetManager;

        private void Awake()
        {
            if (celShadingShader == null)
            {
                celShadingShader = Shader.Find("Lucha3D/AnimeCelShading");
                if (celShadingShader == null)
                {
                    celShadingShader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }

            CreateMaterials();
            SetupLighting();
            SetupVoxelArena();
            SetupPlayerCharacter();
            SetupTrainingDummies();
            SetupDestructibleBuilding();
            SetupThirdPersonCamera();
            SetupHUD();
            SetupArenaResetManager();
        }

        #region Creación de Materiales Estilizados
        private void CreateMaterials()
        {
            // Material Jugador (Cel-shading Cian / Blanco)
            _playerMat = new Material(celShadingShader);
            _playerMat.name = "Mat_Player_Anime";
            _playerMat.SetColor("_BaseColor", new Color(0.2f, 0.85f, 0.95f, 1f));
            _playerMat.SetColor("_ShadowColor", new Color(0.1f, 0.25f, 0.45f, 1f));
            _playerMat.SetFloat("_OutlineWidth", 0.006f);

            // Material Dummy A (Carmesí)
            _enemyMatA = new Material(celShadingShader);
            _enemyMatA.name = "Mat_DummyA_Anime";
            _enemyMatA.SetColor("_BaseColor", new Color(0.95f, 0.25f, 0.35f, 1f));
            _enemyMatA.SetColor("_ShadowColor", new Color(0.4f, 0.08f, 0.15f, 1f));
            _enemyMatA.SetFloat("_OutlineWidth", 0.006f);

            // Material Dummy B (Púrpura / Dorado)
            _enemyMatB = new Material(celShadingShader);
            _enemyMatB.name = "Mat_DummyB_Anime";
            _enemyMatB.SetColor("_BaseColor", new Color(0.7f, 0.3f, 0.95f, 1f));
            _enemyMatB.SetColor("_ShadowColor", new Color(0.25f, 0.05f, 0.4f, 1f));
            _enemyMatB.SetFloat("_OutlineWidth", 0.006f);

            // Material Terreno
            _groundMat = new Material(celShadingShader);
            _groundMat.name = "Mat_Ground_Anime";
            _groundMat.SetColor("_BaseColor", new Color(0.35f, 0.65f, 0.45f, 1f));
            _groundMat.SetColor("_ShadowColor", new Color(0.15f, 0.3f, 0.2f, 1f));
            _groundMat.SetFloat("_OutlineWidth", 0.003f);

            // Material Muro Destructible
            _wallMat = new Material(celShadingShader);
            _wallMat.name = "Mat_Wall_Anime";
            _wallMat.SetColor("_BaseColor", new Color(0.85f, 0.75f, 0.65f, 1f));
            _wallMat.SetColor("_ShadowColor", new Color(0.45f, 0.38f, 0.32f, 1f));
            _wallMat.SetFloat("_OutlineWidth", 0.005f);

            // Material Escombro
            _debrisMat = new Material(celShadingShader);
            _debrisMat.name = "Mat_Debris_Anime";
            _debrisMat.SetColor("_BaseColor", new Color(0.6f, 0.55f, 0.5f, 1f));
            _debrisMat.SetColor("_ShadowColor", new Color(0.25f, 0.22f, 0.2f, 1f));
            _debrisMat.SetFloat("_OutlineWidth", 0.005f);
        }

        private void SetupLighting()
        {
            // Luz direccional estilo anime
            var lightObj = new GameObject("Anime_DirectionalLight");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.98f, 0.92f);
            light.intensity = 1.3f;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
        #endregion

        #region Suelo Vóxel y Arena de Combate
        private void SetupVoxelArena()
        {
            var groundObj = new GameObject("VoxelTerrain_Ground");
            groundObj.transform.position = new Vector3(-16f, 0f, -16f);

            var meshFilter = groundObj.AddComponent<MeshFilter>();
            var meshRenderer = groundObj.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _groundMat;

            var meshCollider = groundObj.AddComponent<MeshCollider>();
            groundObj.AddComponent<VoxelTerrainGrid>();

            // Colisionador de suelo base de seguridad
            var planeCollider = new GameObject("Safety_FloorPlane");
            planeCollider.transform.position = new Vector3(0f, 3.8f, 0f);
            planeCollider.transform.localScale = new Vector3(10f, 1f, 10f);
            var planeCol = planeCollider.AddComponent<BoxCollider>();
            planeCol.size = new Vector3(10f, 0.1f, 10f);
        }
        #endregion

        #region Ensamblaje del Personaje Luchador
        private void SetupPlayerCharacter()
        {
            _playerObj = new GameObject("Player_Luchador");
            _playerObj.transform.position = new Vector3(0f, 4.5f, 0f);

            // 1. Modelo geométrico estilizado
            var mannequin = BuildStylizedMannequin("Visual_Mannequin", _playerMat, isPlayer: true);
            mannequin.transform.SetParent(_playerObj.transform, false);

            // Socket para escombros en la mano
            var handSocket = new GameObject("HandSocket_Debris");
            handSocket.transform.SetParent(_playerObj.transform);
            handSocket.transform.localPosition = new Vector3(0.5f, 1.1f, 0.7f);

            // 2. Colisionador físico
            var cc = _playerObj.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.45f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            // 3. Componentes lógicos
            _playerObj.AddComponent<PlayerInputReader>();
            _playerObj.AddComponent<HealthSystem>();
            _playerObj.AddComponent<StaminaSystem>();
            _playerObj.AddComponent<PlayerLocomotion>();
            _playerObj.AddComponent<PlayerCombatController>();
            _playerObj.AddComponent<BoogieWoogieAbility>();
            _playerObj.AddComponent<ProjectionTechnique>();
            _playerObj.AddComponent<PlayerDebrisHandler>();
            _playerObj.AddComponent<AbilityLoadoutManager>();

            var targetable = _playerObj.AddComponent<TargetableEntity>();
            targetable.name = "Player_Targetable";
        }

        private GameObject BuildStylizedMannequin(string name, Material mat, bool isPlayer)
        {
            var root = new GameObject(name);

            // Torso
            var torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
            torso.name = "Torso";
            torso.transform.SetParent(root.transform);
            torso.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            torso.transform.localScale = new Vector3(0.65f, 0.75f, 0.35f);
            Destroy(torso.GetComponent<Collider>());
            torso.GetComponent<Renderer>().sharedMaterial = mat;

            // Cabeza
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform);
            head.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            head.transform.localScale = new Vector3(0.4f, 0.45f, 0.4f);
            Destroy(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().sharedMaterial = mat;

            // Visor anime / detalle frontal
            var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "AnimeVisor";
            visor.transform.SetParent(head.transform);
            visor.transform.localPosition = new Vector3(0f, 0.1f, 0.45f);
            visor.transform.localScale = new Vector3(0.7f, 0.25f, 0.3f);
            Destroy(visor.GetComponent<Collider>());
            var visorMat = new Material(mat);
            visorMat.SetColor("_BaseColor", isPlayer ? Color.white : Color.black);
            visor.GetComponent<Renderer>().sharedMaterial = visorMat;

            // Brazos
            var leftArm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftArm.name = "LeftArm";
            leftArm.transform.SetParent(root.transform);
            leftArm.transform.localPosition = new Vector3(-0.45f, 1.05f, 0f);
            leftArm.transform.localScale = new Vector3(0.2f, 0.7f, 0.2f);
            Destroy(leftArm.GetComponent<Collider>());
            leftArm.GetComponent<Renderer>().sharedMaterial = mat;

            var rightArm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightArm.name = "RightArm";
            rightArm.transform.SetParent(root.transform);
            rightArm.transform.localPosition = new Vector3(0.45f, 1.05f, 0f);
            rightArm.transform.localScale = new Vector3(0.2f, 0.7f, 0.2f);
            Destroy(rightArm.GetComponent<Collider>());
            rightArm.GetComponent<Renderer>().sharedMaterial = mat;

            // Piernas
            var leftLeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftLeg.name = "LeftLeg";
            leftLeg.transform.SetParent(root.transform);
            leftLeg.transform.localPosition = new Vector3(-0.2f, 0.35f, 0f);
            leftLeg.transform.localScale = new Vector3(0.22f, 0.7f, 0.25f);
            Destroy(leftLeg.GetComponent<Collider>());
            leftLeg.GetComponent<Renderer>().sharedMaterial = mat;

            var rightLeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightLeg.name = "RightLeg";
            rightLeg.transform.SetParent(root.transform);
            rightLeg.transform.localPosition = new Vector3(0.2f, 0.35f, 0f);
            rightLeg.transform.localScale = new Vector3(0.22f, 0.7f, 0.25f);
            Destroy(rightLeg.GetComponent<Collider>());
            rightLeg.GetComponent<Renderer>().sharedMaterial = mat;

            return root;
        }
        #endregion

        #region Maniquíes de Entrenamiento (Objetivos para Boogie-Woogie y Proyección)
        private void SetupTrainingDummies()
        {
            CreateDummy("Dummy_A_Carmesí", new Vector3(0f, 4.5f, 6.0f), _enemyMatA);
            CreateDummy("Dummy_B_Purpura", new Vector3(7f, 4.5f, 4.0f), _enemyMatB);
        }

        private GameObject CreateDummy(string name, Vector3 position, Material mat)
        {
            var dummyObj = new GameObject(name);
            dummyObj.transform.position = position;

            var mannequin = BuildStylizedMannequin("Visual_Dummy", mat, isPlayer: false);
            mannequin.transform.SetParent(dummyObj.transform, false);

            var cc = dummyObj.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.45f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            // Los maniquíes son estáticos: NO llevan PlayerInputReader ni PlayerLocomotion
            dummyObj.AddComponent<TargetableEntity>();
            dummyObj.AddComponent<HealthSystem>(); // 200 HP, 4s delay para regenerar, desaparece al morir
            dummyObj.AddComponent<TrainingDummy>(); // Gestiona impactos, knockback físico amortiguado y daño

            return dummyObj;
        }
        #endregion

        #region Estructura Modular Destructible
        private void SetupDestructibleBuilding()
        {
            var buildingObj = new GameObject("Destructible_ModularWall");
            buildingObj.transform.position = new Vector3(-6f, 4.2f, 5f);

            // 1. Estado intacto
            var intactRoot = new GameObject("Intact_Visual");
            intactRoot.transform.SetParent(buildingObj.transform, false);
            var intactCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            intactCube.name = "Intact_Mesh";
            intactCube.transform.SetParent(intactRoot.transform, false);
            intactCube.transform.localScale = new Vector3(4f, 3.5f, 0.8f);
            intactCube.GetComponent<Renderer>().sharedMaterial = _wallMat;
            var intactCol = intactCube.GetComponent<BoxCollider>();

            // 2. Estado fracturado (bloques modulares con Rigidbodies)
            var fracturedRoot = new GameObject("Fractured_Pieces");
            fracturedRoot.transform.SetParent(buildingObj.transform, false);

            int xCount = 4;
            int yCount = 4;
            float blockWidth = 4f / xCount;
            float blockHeight = 3.5f / yCount;

            for (int x = 0; x < xCount; x++)
            {
                for (int y = 0; y < yCount; y++)
                {
                    var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    piece.name = $"Piece_{x}_{y}";
                    piece.transform.SetParent(fracturedRoot.transform, false);
                    float posX = (x - (xCount - 1) * 0.5f) * blockWidth;
                    float posY = (y - (yCount - 1) * 0.5f) * blockHeight;
                    piece.transform.localPosition = new Vector3(posX, posY, 0f);
                    piece.transform.localScale = new Vector3(blockWidth * 0.95f, blockHeight * 0.95f, 0.75f);
                    piece.GetComponent<Renderer>().sharedMaterial = _wallMat;

                    var rb = piece.AddComponent<Rigidbody>();
                    rb.mass = 20f;
                    rb.isKinematic = true;

                    piece.AddComponent<TargetableEntity>();
                }
            }
            fracturedRoot.SetActive(false);

            // Componente de destrucción
            var destructible = buildingObj.AddComponent<DestructibleStructure>();
            // Los campos se configuran mediante reflexión o Awake
        }
        #endregion

        #region Cámara en Tercera Persona
        private void SetupThirdPersonCamera()
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                var camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                _mainCamera = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }

            var follow = _mainCamera.gameObject.GetComponent<CameraFollow>();
            if (follow == null) follow = _mainCamera.gameObject.AddComponent<CameraFollow>();

            follow.Target = _playerObj.transform;
        }
        #endregion

        #region HUD Visual y Reinicio de Arena
        private void SetupHUD()
        {
            var canvasObj = new GameObject("HUD_Canvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            canvasObj.AddComponent<PlayerHUD>();
        }

        private void SetupArenaResetManager()
        {
            var managerObj = new GameObject("ArenaReset_Manager");
            _resetManager = managerObj.AddComponent<ArenaResetManager>();
        }
        #endregion
    }
}
