using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Lucha3D.Combat;
using Lucha3D.Movement;
using Lucha3D.Core;

namespace Lucha3D.VoxelTerrain
{
    /// <summary>
    /// Gestiona la mecánica de arranque, retención y lanzamiento de escombros de roca del suelo.
    /// Responde al combo 'Agacharse + Ataque Cargado Hacia Abajo'.
    /// </summary>
    [RequireComponent(typeof(PlayerCombatController))]
    [RequireComponent(typeof(PlayerLocomotion))]
    [RequireComponent(typeof(PlayerInputReader))]
    [DisallowMultipleComponent]
    public class PlayerDebrisHandler : MonoBehaviour
    {
        [Header("--- Referencias y Socket de Agarre ---")]
        [Tooltip("Punto de anclaje de la mano donde se sostiene el escombro arrancado.")]
        [SerializeField] private Transform handSocket;

        [Tooltip("Prefab del escombro. Si está vacío, se creará un objeto primitivo rocoso estilizado.")]
        [SerializeField] private GameObject debrisPrefab;

        [Header("--- Parámetros de Lanzamiento ---")]
        [Tooltip("Velocidad inicial del proyectil al ser lanzado (m/s).")]
        [SerializeField] private float throwVelocity = 28f;

        [Tooltip("Ángulo de elevación balística del lanzamiento.")]
        [Range(0f, 30f)]
        [SerializeField] private float throwElevationAngle = 10f;

        private PlayerCombatController _combatController;
        private PlayerLocomotion _locomotion;
        private PlayerInputReader _inputReader;
        private StaminaSystem _staminaSystem;

        private VoxelDebrisProjectile _currentlyHeldDebris;

        #region Eventos Públicos
        public event Action<VoxelDebrisProjectile> OnDebrisPickedUp;
        public event Action<VoxelDebrisProjectile> OnDebrisThrown;
        #endregion

        public bool IsHoldingDebris => _currentlyHeldDebris != null;

        private void Awake()
        {
            _combatController = GetComponent<PlayerCombatController>();
            _locomotion = GetComponent<PlayerLocomotion>();
            _inputReader = GetComponent<PlayerInputReader>();
            _staminaSystem = GetComponent<StaminaSystem>();

            if (handSocket == null)
            {
                // Crear un socket provisional al frente a la derecha si no se ha asignado un hueso de mano
                GameObject socketObj = new GameObject("HandSocket_Debris");
                socketObj.transform.SetParent(transform);
                socketObj.transform.localPosition = new Vector3(0.5f, 1.2f, 0.6f);
                handSocket = socketObj.transform;
            }
        }

        private void OnEnable()
        {
            if (_combatController != null)
            {
                _combatController.OnGroundRipTriggered += HandleGroundRip;
            }
        }

        private void OnDisable()
        {
            if (_combatController != null)
            {
                _combatController.OnGroundRipTriggered -= HandleGroundRip;
            }
        }

        private void Update()
        {
            // Si el jugador sostiene un escombro y pulsa atacar, lo lanza
            if (IsHoldingDebris && _inputReader.WasAttackTriggered)
            {
                ThrowHeldDebris();
            }
        }

        private void HandleGroundRip(Vector3 ripWorldPosition, float chargeDuration, float attackDamage)
        {
            // Si ya tiene un escombro en mano, lo soltamos o ignoramos
            if (IsHoldingDebris) return;

            // Instanciar el escombro
            GameObject debrisInstance;
            if (debrisPrefab != null)
            {
                debrisInstance = Instantiate(debrisPrefab, ripWorldPosition, Quaternion.identity);
            }
            else
            {
                debrisInstance = CreateProceduralRockObject(ripWorldPosition);
            }

            if (debrisInstance.TryGetComponent<VoxelDebrisProjectile>(out var projectile))
            {
                _currentlyHeldDebris = projectile;
                _currentlyHeldDebris.HoldInHand(handSocket, gameObject);
                OnDebrisPickedUp?.Invoke(_currentlyHeldDebris);
            }
        }

        /// <summary>
        /// Lanza la roca sostenida en la dirección que mira el personaje.
        /// </summary>
        public void ThrowHeldDebris()
        {
            if (!IsHoldingDebris) return;

            Vector3 throwDir = transform.forward;
            throwDir = Quaternion.AngleAxis(-throwElevationAngle, transform.right) * throwDir;

            // La fuerza del Índice de Fuerza añade un bono de velocidad y daño al lanzamiento
            float strengthMultiplier = _staminaSystem != null ? _staminaSystem.CurrentStrengthIndex : 1.0f;
            float finalVelocity = throwVelocity * (1f + (strengthMultiplier - 1f) * 0.25f);

            var projectile = _currentlyHeldDebris;
            _currentlyHeldDebris = null;

            projectile.Launch(throwDir, finalVelocity, damageMultiplier: strengthMultiplier);
            OnDebrisThrown?.Invoke(projectile);
        }

        private GameObject CreateProceduralRockObject(Vector3 position)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = "VoxelDebris_Rock";
            rock.transform.position = position;
            rock.transform.localScale = new Vector3(0.9f, 0.8f, 1.1f);

            var rb = rock.GetComponent<Rigidbody>();
            if (rb == null) rb = rock.AddComponent<Rigidbody>();
            rb.mass = 15f;

            rock.AddComponent<VoxelDebrisProjectile>();
            return rock;
        }
    }
}
