using System;
using UnityEngine;

namespace Lucha3D.Core
{
    /// <summary>
    /// Gestiona la estamina del personaje, su regeneración condicional y el cálculo
    /// del Índice de Fuerza matemático (x1 a x4).
    /// </summary>
    [DisallowMultipleComponent]
    public class StaminaSystem : MonoBehaviour
    {
        [Header("--- Parámetros de Estamina ---")]
        [Tooltip("Puntos máximos de estamina.")]
        [SerializeField] private float maxStamina = 300f;

        [Tooltip("Velocidad de regeneración pasiva (puntos por segundo).")]
        [SerializeField] private float baseRegenRate = 30f;

        [Tooltip("Tiempo de espera tras cesar acciones bloqueantes antes de reanudar la regeneración (segundos).")]
        [SerializeField] private float regenCooldown = 0.5f;

        [Header("--- Parámetros del Índice de Fuerza ---")]
        [Tooltip("Umbral inferior de estamina: por debajo o igual a este valor, la fuerza es fija en x1.")]
        [SerializeField] private float lowStaminaThreshold = 30f;

        [Tooltip("Umbral superior de estamina: por encima o igual a este valor, la fuerza es fija en x4.")]
        [SerializeField] private float highStaminaThreshold = 250f;

        [Tooltip("Constante de curvatura (k) para la función exponencial invertida en el rango medio.")]
        [Range(1f, 10f)]
        [SerializeField] private float exponentialCurvatureK = 3.5f;

        [Tooltip("Si se activa, utiliza la curva de animación personalizada en lugar de la fórmula matemática pura.")]
        [SerializeField] private bool useCustomCurve = false;

        [Tooltip("Curva visual de evaluación de fuerza para ajuste fino desde el Inspector.")]
        [SerializeField] private AnimationCurve customStrengthCurve;

        // Estado interno en tiempo de ejecución
        private float _currentStamina;
        private float _currentStrengthIndex;
        private float _currentCooldownTimer;
        private int _regenBlockersCount;

        #region Eventos Públicos (Arquitectura Desacoplada)
        /// <summary>
        /// Notifica cambios en el valor de estamina. Parámetros: (estaminaActual, estaminaMaxima).
        /// </summary>
        public event Action<float, float> OnStaminaChanged;

        /// <summary>
        /// Notifica cambios en el multiplicador del Índice de Fuerza. Parámetro: (nuevoIndiceFuerza).
        /// </summary>
        public event Action<float> OnStrengthIndexChanged;

        /// <summary>
        /// Notifica si la regeneración está activa o bloqueada en este momento. Parámetro: (isRegenerating).
        /// </summary>
        public event Action<bool> OnRegenStateChanged;
        #endregion

        #region Propiedades Públicas
        public float CurrentStamina => _currentStamina;
        public float MaxStamina => maxStamina;
        public float CurrentStrengthIndex => _currentStrengthIndex;
        public bool IsRegenerating => _regenBlockersCount == 0 && _currentCooldownTimer <= 0f && _currentStamina < maxStamina;
        public bool IsRegenBlocked => _regenBlockersCount > 0;
        public float CooldownRemaining => _currentCooldownTimer;
        #endregion

        private void Awake()
        {
            _currentStamina = maxStamina;
            _currentStrengthIndex = CalculateStrengthIndex(_currentStamina);
            _currentCooldownTimer = 0f;
            _regenBlockersCount = 0;
        }

        private void Start()
        {
            // Emisión inicial para que la UI o los sistemas suscritos sincronicen su estado en el frame 0
            OnStaminaChanged?.Invoke(_currentStamina, maxStamina);
            OnStrengthIndexChanged?.Invoke(_currentStrengthIndex);
            OnRegenStateChanged?.Invoke(IsRegenerating);
        }

        private void Update()
        {
            HandleRegeneration();
        }

        #region Lógica de Regeneración y Cooldown
        private void HandleRegeneration()
        {
            // Si hay acciones activas bloqueando (sprint, ataque, carga, habilidad)
            if (_regenBlockersCount > 0)
            {
                // Mantenemos el temporizador reseteado en su valor completo mientras haya bloqueos
                _currentCooldownTimer = regenCooldown;
                return;
            }

            // Si no hay bloqueos pero el temporizador de cooldown sigue corriendo
            if (_currentCooldownTimer > 0f)
            {
                _currentCooldownTimer -= Time.deltaTime;
                if (_currentCooldownTimer <= 0f)
                {
                    _currentCooldownTimer = 0f;
                    OnRegenStateChanged?.Invoke(IsRegenerating);
                }
                return;
            }

            // Regeneración activa
            if (_currentStamina < maxStamina)
            {
                float prevStamina = _currentStamina;
                _currentStamina = Mathf.Min(_currentStamina + baseRegenRate * Time.deltaTime, maxStamina);

                if (!Mathf.Approximately(prevStamina, _currentStamina))
                {
                    UpdateStrengthAndNotify();
                }
            }
        }

        /// <summary>
        /// Agrega un bloqueo de regeneración (debe llamarse al iniciar sprint, animación de ataque, carga o habilidad).
        /// Admite múltiples bloqueos concurrentes sin interferir entre sí.
        /// </summary>
        public void AddRegenBlocker()
        {
            bool wasRegenerating = IsRegenerating;
            _regenBlockersCount++;
            _currentCooldownTimer = regenCooldown;

            if (wasRegenerating != IsRegenerating)
            {
                OnRegenStateChanged?.Invoke(IsRegenerating);
            }
        }

        /// <summary>
        /// Retira un bloqueo de regeneración (debe llamarse al terminar la acción bloqueante).
        /// Cuando todos los bloqueos finalizan, comienza el retraso de cooldown (0.5s).
        /// </summary>
        public void RemoveRegenBlocker()
        {
            if (_regenBlockersCount > 0)
            {
                _regenBlockersCount--;
            }

            if (_regenBlockersCount == 0)
            {
                _currentCooldownTimer = regenCooldown;
                OnRegenStateChanged?.Invoke(IsRegenerating);
            }
        }
        #endregion

        #region Verificación y Consumo Transaccional
        /// <summary>
        /// Comprueba si el personaje dispone de la estamina requerida sin consumirla.
        /// </summary>
        public bool HasEnoughStamina(float amount)
        {
            return _currentStamina >= amount;
        }

        /// <summary>
        /// Ejecuta el consumo seguro bajo el patrón Check-Then-Consume:
        /// Si hay suficiente estamina, la descuenta de inmediato y devuelve true.
        /// Si no alcanza, no descuenta nada y devuelve false.
        /// </summary>
        public bool TryConsumeStamina(float amount)
        {
            if (amount <= 0f) return true;

            if (_currentStamina >= amount)
            {
                _currentStamina -= amount;
                UpdateStrengthAndNotify();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Consumo directo para casos continuos donde la comprobación ya se realizó externamente (ej: sprint por tick).
        /// </summary>
        public void ConsumeStaminaUnchecked(float amount)
        {
            if (amount <= 0f) return;

            _currentStamina = Mathf.Max(0f, _currentStamina - amount);
            UpdateStrengthAndNotify();
        }

        /// <summary>
        /// Restaura estamina manualmente (útil para orbes, mecánicas especiales o reinicios).
        /// </summary>
        public void RestoreStamina(float amount)
        {
            if (amount <= 0f) return;

            _currentStamina = Mathf.Min(maxStamina, _currentStamina + amount);
            UpdateStrengthAndNotify();
        }
        #endregion

        #region Cálculo Matemático del Índice de Fuerza
        /// <summary>
        /// Calcula el multiplicador de fuerza (x1 a x4) en base a la estamina actual.
        /// - [0, 30]: Fijo x1
        /// - [250, 300]: Fijo x4
        /// - (30, 250): Curva exponencial invertida cóncava.
        /// </summary>
        public float CalculateStrengthIndex(float stamina)
        {
            // Rango Bajo
            if (stamina <= lowStaminaThreshold)
            {
                return 1.0f;
            }

            // Rango Alto
            if (stamina >= highStaminaThreshold)
            {
                return 4.0f;
            }

            // Rango Medio: Normalizado entre 0 (en 30 pts) y 1 (en 250 pts)
            float u = (stamina - lowStaminaThreshold) / (highStaminaThreshold - lowStaminaThreshold);

            if (useCustomCurve && customStrengthCurve != null && customStrengthCurve.length > 0)
            {
                return Mathf.Lerp(1.0f, 4.0f, customStrengthCurve.Evaluate(u));
            }

            // Fórmula Analítica: F(u) = 1 + 3 * ( (1 - e^(-k * u)) / (1 - e^(-k)) )
            // Esta función es cóncava hacia abajo:
            // Al bajar desde 250 (u=1), se mantiene alta y cae muy despacio.
            // Al aproximarse a 30 (u=0), cae en picado hacia x1.
            float k = exponentialCurvatureK;
            float denominator = 1.0f - Mathf.Exp(-k);
            float numerator = 1.0f - Mathf.Exp(-k * u);
            float normalizedFactor = numerator / denominator;

            return 1.0f + 3.0f * normalizedFactor;
        }

        private void UpdateStrengthAndNotify()
        {
            float oldStrength = _currentStrengthIndex;
            _currentStrengthIndex = CalculateStrengthIndex(_currentStamina);

            OnStaminaChanged?.Invoke(_currentStamina, maxStamina);

            if (!Mathf.Approximately(oldStrength, _currentStrengthIndex))
            {
                OnStrengthIndexChanged?.Invoke(_currentStrengthIndex);
            }
        }
        #endregion

        #region Editor y Validación
        private void OnValidate()
        {
            if (maxStamina < 1f) maxStamina = 1f;
            if (lowStaminaThreshold < 0f) lowStaminaThreshold = 0f;
            if (highStaminaThreshold <= lowStaminaThreshold) highStaminaThreshold = lowStaminaThreshold + 1f;
            if (highStaminaThreshold > maxStamina) highStaminaThreshold = maxStamina;
            if (exponentialCurvatureK < 0.1f) exponentialCurvatureK = 0.1f;

            if (customStrengthCurve == null || customStrengthCurve.length == 0)
            {
                InitializeDefaultCurve();
            }
        }

        private void Reset()
        {
            maxStamina = 300f;
            baseRegenRate = 30f;
            regenCooldown = 0.5f;
            lowStaminaThreshold = 30f;
            highStaminaThreshold = 250f;
            exponentialCurvatureK = 3.5f;
            useCustomCurve = false;

            InitializeDefaultCurve();
        }

        private void InitializeDefaultCurve()
        {
            // Pre-construye una curva idéntica a la fórmula analítica con tangentes suaves
            customStrengthCurve = new AnimationCurve();
            customStrengthCurve.AddKey(new Keyframe(0f, 0f, 3.5f, 3.5f));
            customStrengthCurve.AddKey(new Keyframe(0.2f, 0.52f, 1.8f, 1.8f));
            customStrengthCurve.AddKey(new Keyframe(0.5f, 0.85f, 0.7f, 0.7f));
            customStrengthCurve.AddKey(new Keyframe(0.8f, 0.97f, 0.2f, 0.2f));
            customStrengthCurve.AddKey(new Keyframe(1f, 1f, 0.05f, 0.05f));
        }
        #endregion
    }
}
