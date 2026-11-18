using System;
using UnityEngine;

namespace Lucha3D.Core
{
    /// <summary>
    /// Sistema de salud modular e independiente (200 HP).
    /// - Salud máxima: 200 puntos.
    /// - Al llegar a 0 de vida, el objeto desaparece.
    /// - Si pasa más de 4 segundos sin recibir daño, regenera pasivamente 20 puntos por segundo.
    /// </summary>
    [DisallowMultipleComponent]
    public class HealthSystem : MonoBehaviour
    {
        [Header("--- Parámetros de Salud ---")]
        [Tooltip("Salud máxima del personaje o maniquí.")]
        [SerializeField] private float maxHealth = 200f;

        [Tooltip("Tiempo necesario sin recibir impactos para iniciar la regeneración (segundos).")]
        [SerializeField] private float regenDelay = 4.0f;

        [Tooltip("Velocidad de regeneración pasiva de vida (puntos por segundo).")]
        [SerializeField] private float regenRate = 20.0f;

        [Tooltip("Si es verdadero, desactiva el GameObject al morir. Si es falso, solo emite el evento OnDeath.")]
        [SerializeField] private bool deactivateOnDeath = true;

        // Estado en tiempo de ejecución
        private float _currentHealth;
        private float _timeSinceLastDamage;
        private bool _isDead = false;

        #region Eventos Públicos (Arquitectura Desacoplada)
        /// <summary>
        /// Se dispara al variar la vida actual. Parámetros: (vidaActual, vidaMaxima).
        /// </summary>
        public event Action<float, float> OnHealthChanged;

        /// <summary>
        /// Se dispara al recibir daño. Parámetro: (cantidadDaño).
        /// </summary>
        public event Action<float> OnDamaged;

        /// <summary>
        /// Se dispara cuando la vida se reduce a 0.
        /// </summary>
        public event Action OnDeath;

        /// <summary>
        /// Se dispara cuando la regeneración pasiva está activa y curando.
        /// </summary>
        public event Action<float> OnHealed;
        #endregion

        #region Propiedades Públicas
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => maxHealth;
        public float HealthPercent => Mathf.Clamp01(_currentHealth / maxHealth);
        public bool IsDead => _isDead;
        public bool IsRegenerating => !_isDead && _timeSinceLastDamage >= regenDelay && _currentHealth < maxHealth;
        public float TimeSinceLastDamage => _timeSinceLastDamage;
        #endregion

        private void Awake()
        {
            _currentHealth = maxHealth;
            _timeSinceLastDamage = regenDelay; // Inicia listo para estar al máximo
            _isDead = false;
        }

        private void Start()
        {
            OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        }

        private void Update()
        {
            if (_isDead) return;

            HandlePassiveRegeneration();
        }

        private void HandlePassiveRegeneration()
        {
            _timeSinceLastDamage += Time.deltaTime;

            // Tras 4 segundos sin daño, inicia la regeneración de 20 pts/s
            if (_timeSinceLastDamage >= regenDelay && _currentHealth < maxHealth)
            {
                float prevHealth = _currentHealth;
                _currentHealth = Mathf.Min(maxHealth, _currentHealth + regenRate * Time.deltaTime);

                if (!Mathf.Approximately(prevHealth, _currentHealth))
                {
                    OnHealthChanged?.Invoke(_currentHealth, maxHealth);
                    OnHealed?.Invoke(_currentHealth - prevHealth);
                }
            }
        }

        /// <summary>
        /// Aplica daño a la entidad, resetea el temporizador de 4 segundos y comprueba la muerte.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (_isDead || amount <= 0f) return;

            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
            _timeSinceLastDamage = 0f; // Reinicia el temporizador de los 4 segundos

            OnDamaged?.Invoke(amount);
            OnHealthChanged?.Invoke(_currentHealth, maxHealth);

            if (_currentHealth <= 0f)
            {
                Die();
            }
        }

        /// <summary>
        /// Cura vida manualmente.
        /// </summary>
        public void Heal(float amount)
        {
            if (_isDead || amount <= 0f) return;

            _currentHealth = Mathf.Min(maxHealth, _currentHealth + amount);
            OnHealthChanged?.Invoke(_currentHealth, maxHealth);
            OnHealed?.Invoke(amount);
        }

        private void Die()
        {
            _isDead = true;
            OnDeath?.Invoke();

            if (deactivateOnDeath)
            {
                // Hace desaparecer la entidad al acabarse la vida
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Restaura la vida completa y revive a la entidad (usado en el reinicio de arena).
        /// </summary>
        public void ResetHealth()
        {
            _isDead = false;
            _currentHealth = maxHealth;
            _timeSinceLastDamage = regenDelay;

            if (deactivateOnDeath && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        }

        private void OnValidate()
        {
            if (maxHealth < 1f) maxHealth = 1f;
            if (regenDelay < 0f) regenDelay = 0f;
            if (regenRate < 0f) regenRate = 0f;
        }
    }
}
