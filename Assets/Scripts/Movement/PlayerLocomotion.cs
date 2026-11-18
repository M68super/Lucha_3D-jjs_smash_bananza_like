using System;
using UnityEngine;
using Lucha3D.Core;

namespace Lucha3D.Movement
{
    public enum LocomotionMode
    {
        Walk,
        Run,
        Sprint
    }

    /// <summary>
    /// Controlador de locomoción 3D basado en CharacterController.
    /// Implementa las 3 velocidades exactas (Andar 3 km/h, Correr 10 km/h, Sprint 15 km/h base * Fuerza),
    /// la regla transaccional de consumo de estamina, el soporte de slow-fall para ataques aéreos cargados,
    /// y la absorción de impulsos/knockback.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(StaminaSystem))]
    [DisallowMultipleComponent]
    public class PlayerLocomotion : MonoBehaviour
    {
        #region Constantes de Conversión de Velocidad (km/h a m/s)
        // 1 km/h = 1000m / 3600s = 1 / 3.6 m/s
        private const float KMH_TO_MPS = 1f / 3.6f;

        // Velocidades fijas de diseño
        public const float WALK_SPEED_KMH = 3.0f;
        public const float RUN_SPEED_KMH = 10.0f;
        public const float SPRINT_BASE_SPEED_KMH = 15.0f;

        public static readonly float WalkSpeedMps = WALK_SPEED_KMH * KMH_TO_MPS;       // ~0.833 m/s
        public static readonly float RunSpeedMps = RUN_SPEED_KMH * KMH_TO_MPS;         // ~2.778 m/s
        public static readonly float SprintBaseSpeedMps = SPRINT_BASE_SPEED_KMH * KMH_TO_MPS; // ~4.167 m/s
        #endregion

        [Header("--- Referencias Internas ---")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private StaminaSystem staminaSystem;
        [SerializeField] private Transform cameraTransform;

        [Header("--- Parámetros de Giro y Suelo ---")]
        [Tooltip("Velocidad de rotación angular hacia la dirección de avance.")]
        [SerializeField] private float rotationSpeed = 720f;

        [Tooltip("Suavizado de aceleración/deceleración.")]
        [SerializeField] private float accelerationRate = 20f;

        [Header("--- Parámetros de Salto y Gravedad ---")]
        [Tooltip("Fuerza de la gravedad estándar (m/s²).")]
        [SerializeField] private float standardGravity = 25f;

        [Tooltip("Altura máxima del salto en metros.")]
        [SerializeField] private float jumpHeight = 2.2f;

        [Tooltip("Multiplicador de gravedad cuando el jugador activa caída ralentizada (ataque aéreo cargado).")]
        [Range(0.01f, 0.5f)]
        [SerializeField] private float slowFallGravityMultiplier = 0.15f;

        [Tooltip("Velocidad máxima de caída vertical durante slow-fall.")]
        [SerializeField] private float slowFallTerminalVelocity = -2.0f;

        [Header("--- Reglas de Consumo de Sprint ---")]
        private const float SPRINT_INITIAL_COST = 1.0f;
        private const float SPRINT_TICK_COST = 1.0f;
        private const float SPRINT_TICK_INTERVAL = 0.1f; // 1 pt cada 0.1s (10 pts/segundo)

        // Estado de movimiento
        private LocomotionMode _currentMode = LocomotionMode.Run;
        private bool _isSprinting = false;
        private float _sprintTickTimer = 0f;
        private float _currentHorizontalSpeed = 0f;
        private Vector3 _verticalVelocity = Vector3.zero;
        private Vector3 _externalKnockback = Vector3.zero;

        // Modificadores de estado
        private bool _isSlowFallActive = false;
        private bool _areControlsFrozen = false;

        #region Eventos Públicos
        public event Action<LocomotionMode> OnLocomotionModeChanged;
        public event Action OnJumpExecuted;
        public event Action<bool> OnSprintingStateChanged;
        #endregion

        #region Propiedades Públicas
        public LocomotionMode CurrentMode => _currentMode;
        public bool IsSprinting => _isSprinting;
        public bool IsGrounded => characterController.isGrounded;
        public bool AreControlsFrozen => _areControlsFrozen;
        public float CurrentVelocityMagnitude => characterController.velocity.magnitude;
        public Vector3 Velocity => characterController.velocity;
        #endregion

        private void Reset()
        {
            characterController = GetComponent<CharacterController>();
            inputReader = GetComponent<PlayerInputReader>();
            staminaSystem = GetComponent<StaminaSystem>();
        }

        private void Awake()
        {
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (inputReader == null) inputReader = GetComponent<PlayerInputReader>();
            if (staminaSystem == null) staminaSystem = GetComponent<StaminaSystem>();

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void OnEnable()
        {
            if (inputReader != null)
            {
                inputReader.OnJumpPressed += HandleJumpRequest;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.OnJumpPressed -= HandleJumpRequest;
            }

            if (_isSprinting)
            {
                StopSprinting();
            }
        }

        private void Update()
        {
            HandleSprintStaminaLogic();
            HandleHorizontalMovement();
            HandleVerticalPhysics();
            ApplyCombinedMotion();
        }

        #region Lógica de Sprint y Consumo de Estamina
        private void HandleSprintStaminaLogic()
        {
            if (_areControlsFrozen)
            {
                if (_isSprinting) StopSprinting();
                return;
            }

            bool wantsToSprint = inputReader.IsSprintHeld && inputReader.MoveInput.sqrMagnitude > 0.01f;

            if (wantsToSprint)
            {
                if (!_isSprinting)
                {
                    // Intento de inicio de sprint: Requiere 1 pt transaccional
                    if (staminaSystem.TryConsumeStamina(SPRINT_INITIAL_COST))
                    {
                        StartSprinting();
                    }
                }
                else
                {
                    // Sprint sostenido: 1 pt por cada 0.1s
                    _sprintTickTimer += Time.deltaTime;
                    while (_sprintTickTimer >= SPRINT_TICK_INTERVAL)
                    {
                        _sprintTickTimer -= SPRINT_TICK_INTERVAL;
                        if (!staminaSystem.TryConsumeStamina(SPRINT_TICK_COST))
                        {
                            // Estamina agotada: forzar parada de sprint inmediatamente
                            StopSprinting();
                            break;
                        }
                    }
                }
            }
            else
            {
                if (_isSprinting)
                {
                    StopSprinting();
                }
            }
        }

        private void StartSprinting()
        {
            _isSprinting = true;
            _sprintTickTimer = 0f;
            staminaSystem.AddRegenBlocker(); // Congela la regeneración de estamina
            SetLocomotionMode(LocomotionMode.Sprint);
            OnSprintingStateChanged?.Invoke(true);
        }

        private void StopSprinting()
        {
            if (!_isSprinting) return;

            _isSprinting = false;
            _sprintTickTimer = 0f;
            staminaSystem.RemoveRegenBlocker(); // Inicia cooldown de 0.5s para reanudar regeneración
            SetLocomotionMode(inputReader.IsWalkHeld ? LocomotionMode.Walk : LocomotionMode.Run);
            OnSprintingStateChanged?.Invoke(false);
        }

        private void SetLocomotionMode(LocomotionMode newMode)
        {
            if (_currentMode != newMode)
            {
                _currentMode = newMode;
                OnLocomotionModeChanged?.Invoke(_currentMode);
            }
        }
        #endregion

        #region Movimiento Horizontal y Rotación
        private void HandleHorizontalMovement()
        {
            Vector2 rawInput = _areControlsFrozen ? Vector2.zero : inputReader.MoveInput;
            bool hasInput = rawInput.sqrMagnitude > 0.01f;

            // Determinar modo activo si no está sprintando
            if (!_isSprinting)
            {
                if (inputReader.IsWalkHeld)
                {
                    SetLocomotionMode(LocomotionMode.Walk);
                }
                else
                {
                    SetLocomotionMode(LocomotionMode.Run);
                }
            }

            // Cálculo de velocidad objetivo según las reglas matemáticas:
            // - Andar: Fijo 3 km/h
            // - Correr: Fijo 10 km/h
            // - Sprint: 15 km/h base * Índice de Fuerza (x1 a x4)
            float targetSpeed = 0f;
            if (hasInput)
            {
                targetSpeed = _currentMode switch
                {
                    LocomotionMode.Walk => WalkSpeedMps,
                    LocomotionMode.Run => RunSpeedMps,
                    LocomotionMode.Sprint => SprintBaseSpeedMps * staminaSystem.CurrentStrengthIndex,
                    _ => RunSpeedMps
                };
            }

            // Suavizado de velocidad horizontal
            _currentHorizontalSpeed = Mathf.MoveTowards(_currentHorizontalSpeed, targetSpeed, accelerationRate * Time.deltaTime);

            // Obtener dirección relativa a la cámara
            Vector3 targetDirection = CalculateCameraRelativeDirection(rawInput);

            // Rotación suave del personaje hacia la dirección de desplazamiento
            if (hasInput && targetDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        private Vector3 CalculateCameraRelativeDirection(Vector2 input)
        {
            if (cameraTransform == null)
            {
                return new Vector3(input.x, 0f, input.y).normalized;
            }

            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;

            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            return (camForward * input.y + camRight * input.x).normalized;
        }
        #endregion

        #region Salto, Gravedad y Slow-Fall
        private void HandleJumpRequest()
        {
            if (_areControlsFrozen) return;

            if (characterController.isGrounded)
            {
                // Fórmula de salto balístico: v = sqrt(2 * g * h)
                _verticalVelocity.y = Mathf.Sqrt(2f * standardGravity * jumpHeight);
                OnJumpExecuted?.Invoke();
            }
        }

        private void HandleVerticalPhysics()
        {
            if (characterController.isGrounded)
            {
                // Mantener al personaje anclado al suelo cuando no está en salto
                if (_verticalVelocity.y < 0f)
                {
                    _verticalVelocity.y = -2f;
                }
            }
            else
            {
                // Si la caída lenta está activa (carga aérea), amortiguamos la gravedad fuertemente
                float activeGravity = standardGravity;
                if (_isSlowFallActive && _verticalVelocity.y < 0f)
                {
                    activeGravity *= slowFallGravityMultiplier;
                }

                _verticalVelocity.y -= activeGravity * Time.deltaTime;

                // Si slow-fall está activo, acotamos la velocidad terminal de descenso
                if (_isSlowFallActive && _verticalVelocity.y < slowFallTerminalVelocity)
                {
                    _verticalVelocity.y = slowFallTerminalVelocity;
                }
            }
        }
        #endregion

        #region Knockback y Desplazamiento Final
        private void ApplyCombinedMotion()
        {
            // Amortiguación del knockback externo
            if (_externalKnockback.sqrMagnitude > 0.001f)
            {
                _externalKnockback = Vector3.Lerp(_externalKnockback, Vector3.zero, 8f * Time.deltaTime);
            }
            else
            {
                _externalKnockback = Vector3.zero;
            }

            // Dirección horizontal activa orientada por el transform.forward si hay velocidad
            Vector3 horizontalMotion = transform.forward * _currentHorizontalSpeed;

            // Movimiento vectorial final
            Vector3 finalMotion = (horizontalMotion + _verticalVelocity + _externalKnockback) * Time.deltaTime;
            characterController.Move(finalMotion);
        }

        /// <summary>
        /// Aplica una fuerza impulsiva (knockback o impacto Smash).
        /// </summary>
        public void ApplyKnockback(Vector3 impulse)
        {
            _externalKnockback += impulse;
        }

        /// <summary>
        /// Activa o desactiva la caída ralentizada durante ataques aéreos cargados.
        /// </summary>
        public void SetSlowFallGravity(bool enabled)
        {
            _isSlowFallActive = enabled;
        }

        /// <summary>
        /// Congela o libera el control motor del jugador (usado durante la preparación de la Técnica de Proyección).
        /// </summary>
        public void SetControlsFrozen(bool frozen)
        {
            _areControlsFrozen = frozen;
            if (frozen)
            {
                _currentHorizontalSpeed = 0f;
                if (_isSprinting) StopSprinting();
            }
        }
        #endregion
    }
}
