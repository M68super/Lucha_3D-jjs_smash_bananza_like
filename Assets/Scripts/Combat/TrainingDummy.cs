using UnityEngine;
using Lucha3D.Abilities;
using Lucha3D.Core;

namespace Lucha3D.Combat
{
    /// <summary>
    /// Maniquí de entrenamiento o enemigo de prueba estático.
    /// - No posee controles de jugador (permanece completamente estático).
    /// - Integra HealthSystem (200 HP, se desactiva/muere al llegar a 0).
    /// - Reacciona a knockbacks de combate desplazándose suavemente.
    /// - Parpadea en blanco al recibir impactos.
    /// </summary>
    [RequireComponent(typeof(TargetableEntity))]
    [RequireComponent(typeof(HealthSystem))]
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class TrainingDummy : MonoBehaviour
    {
        [Header("--- Estado del Maniquí ---")]
        [SerializeField] private float currentDamageAccumulated = 0f;
        [SerializeField] private Vector3 spawnPosition;
        [SerializeField] private float knockbackDamping = 8f;

        private TargetableEntity _targetable;
        private HealthSystem _healthSystem;
        private CharacterController _characterController;
        private Renderer[] _renderers;
        private Color[] _originalColors;
        private Vector3 _knockbackVelocity = Vector3.zero;

        public float DamageAccumulated => currentDamageAccumulated;
        public HealthSystem Health => _healthSystem;

        private void Awake()
        {
            _targetable = GetComponent<TargetableEntity>();
            _healthSystem = GetComponent<HealthSystem>();
            _characterController = GetComponent<CharacterController>();
            spawnPosition = transform.position;

            _renderers = GetComponentsInChildren<Renderer>();
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i].material.HasProperty("_BaseColor"))
                {
                    _originalColors[i] = _renderers[i].material.GetColor("_BaseColor");
                }
            }
        }

        private void Update()
        {
            // Amortiguación del knockback pasivo para maniquíes estáticos
            if (_knockbackVelocity.sqrMagnitude > 0.001f)
            {
                if (_characterController != null && _characterController.enabled)
                {
                    _characterController.Move(_knockbackVelocity * Time.deltaTime);
                }
                _knockbackVelocity = Vector3.Lerp(_knockbackVelocity, Vector3.zero, knockbackDamping * Time.deltaTime);
            }
            else
            {
                _knockbackVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Aplica daño al HealthSystem, añade knockback y parpadeo visual.
        /// </summary>
        public void TakeHit(float damage, Vector3 knockback)
        {
            currentDamageAccumulated += damage;

            if (_healthSystem != null)
            {
                _healthSystem.TakeDamage(damage);
            }

            _knockbackVelocity += knockback;
            FlashHitColor();

            Debug.Log($"[{gameObject.name}] Daño: {damage} (Vida restante: {_healthSystem?.CurrentHealth:F0} / {_healthSystem?.MaxHealth:F0}). Acumulado: {currentDamageAccumulated}%");
        }

        public void ApplyKnockback(Vector3 knockback)
        {
            _knockbackVelocity += knockback;
        }

        private void FlashHitColor()
        {
            foreach (var r in _renderers)
            {
                if (r != null && r.material.HasProperty("_BaseColor"))
                {
                    r.material.SetColor("_BaseColor", Color.white);
                }
            }
            CancelInvoke(nameof(RestoreColor));
            Invoke(nameof(RestoreColor), 0.12f);
        }

        private void RestoreColor()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null && _renderers[i].material.HasProperty("_BaseColor"))
                {
                    _renderers[i].material.SetColor("_BaseColor", _originalColors[i]);
                }
            }
        }

        public void ResetDummy()
        {
            currentDamageAccumulated = 0f;
            _knockbackVelocity = Vector3.zero;

            if (_characterController != null) _characterController.enabled = false;
            transform.position = spawnPosition;
            if (_characterController != null) _characterController.enabled = true;

            if (_healthSystem != null)
            {
                _healthSystem.ResetHealth();
            }
        }
    }
}
