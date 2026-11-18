using System;
using UnityEngine;
using Lucha3D.VoxelTerrain;
using Lucha3D.Destruction;

namespace Lucha3D.Core
{
    /// <summary>
    /// Gestiona el ciclo de vida de la arena de combate.
    /// Ejecuta un reinicio íntegro del mapa cada 5 minutos (300 segundos):
    /// restaura la malla y densidades de Marching Cubes del suelo,
    /// reconstruye todos los edificios y muros destructibles y limpia escombros sueltos.
    /// </summary>
    [DisallowMultipleComponent]
    public class ArenaResetManager : MonoBehaviour
    {
        public const float ARENA_RESET_INTERVAL = 300.0f; // 5 minutos exactos

        [Header("--- Referencias de Arena ---")]
        [Tooltip("Referencia a la cuadrícula de terreno voxelizado.")]
        [SerializeField] private VoxelTerrainGrid terrainGrid;

        [Tooltip("Lista o búsqueda automática de todas las estructuras destructibles.")]
        [SerializeField] private DestructibleStructure[] destructibleStructures;

        [Tooltip("¿Iniciar la cuenta regresiva automáticamente al arrancar la partida?")]
        [SerializeField] private bool autoStartTimer = true;

        private float _timeRemaining;
        private bool _isTimerRunning;

        #region Eventos Públicos
        /// <summary>
        /// Se actualiza continuamente con los segundos restantes para el reinicio.
        /// </summary>
        public event Action<float> OnTimerTick;

        /// <summary>
        /// Se dispara al ejecutarse el reinicio completo de la arena.
        /// </summary>
        public event Action OnArenaReset;
        #endregion

        #region Propiedades Públicas
        public float TimeRemaining => _timeRemaining;
        public float ResetInterval => ARENA_RESET_INTERVAL;
        public bool IsTimerRunning => _isTimerRunning;
        #endregion

        private void Awake()
        {
            if (terrainGrid == null) terrainGrid = FindFirstObjectByType<VoxelTerrainGrid>();
            if (destructibleStructures == null || destructibleStructures.Length == 0)
            {
                destructibleStructures = FindObjectsByType<DestructibleStructure>(FindObjectsSortMode.None);
            }

            _timeRemaining = ARENA_RESET_INTERVAL;
        }

        private void Start()
        {
            if (autoStartTimer)
            {
                _isTimerRunning = true;
            }
        }

        private void Update()
        {
            if (!_isTimerRunning) return;

            _timeRemaining -= Time.deltaTime;
            OnTimerTick?.Invoke(_timeRemaining);

            if (_timeRemaining <= 0f)
            {
                _timeRemaining = ARENA_RESET_INTERVAL;
                ExecuteArenaReset();
            }
        }

        /// <summary>
        /// Ejecuta el reinicio completo de todos los elementos del mapa.
        /// </summary>
        [ContextMenu("Forzar Reinicio de Arena")]
        public void ExecuteArenaReset()
        {
            // 1. Restaurar la topología del terreno de Marching Cubes
            if (terrainGrid != null)
            {
                terrainGrid.ResetTerrain();
            }

            // 2. Reconstruir todos los edificios y muros modulares
            if (destructibleStructures != null)
            {
                foreach (var structure in destructibleStructures)
                {
                    if (structure != null)
                    {
                        structure.ResetStructure();
                    }
                }
            }

            // 3. Limpiar escombros y proyectiles sueltos en el mapa
            var looseDebris = FindObjectsByType<VoxelDebrisProjectile>(FindObjectsSortMode.None);
            foreach (var debris in looseDebris)
            {
                if (debris != null && debris.IsThrown)
                {
                    Destroy(debris.gameObject);
                }
            }

            // 4. Restaurar la estamina de los luchadores en la arena
            var staminaSystems = FindObjectsByType<StaminaSystem>(FindObjectsSortMode.None);
            foreach (var stamina in staminaSystems)
            {
                if (stamina != null)
                {
                    stamina.RestoreStamina(stamina.MaxStamina);
                }
            }

            // 5. Restaurar la salud y revivir a todos los personajes y maniquíes eliminados
            var healthSystems = FindObjectsByType<HealthSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var hp in healthSystems)
            {
                if (hp != null)
                {
                    hp.ResetHealth();
                }
            }

            var dummies = FindObjectsByType<Combat.TrainingDummy>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var dummy in dummies)
            {
                if (dummy != null)
                {
                    dummy.ResetDummy();
                }
            }

            OnArenaReset?.Invoke();
            Debug.Log("[ArenaResetManager] Arena reiniciada por completo: Terreno, Estructuras, Vida y Recursos restaurados.");
        }

        /// <summary>
        /// Pausa o reanuda el temporizador de 5 minutos.
        /// </summary>
        public void SetTimerPaused(bool paused)
        {
            _isTimerRunning = !paused;
        }
    }
}
