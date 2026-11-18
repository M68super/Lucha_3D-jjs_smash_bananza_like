using System;
using System.Collections.Generic;
using UnityEngine;
using Lucha3D.Abilities;

namespace Lucha3D.Destruction
{
    /// <summary>
    /// Estructura modular destructible (muro o edificio).
    /// En estado intacto actúa como una pieza sólida rígida unificada.
    /// Al recibir daño suficiente, se fractura en fragmentos independientes con físicas activas (Rigidbodies),
    /// que además son intercambiables con Boogie-Woogie. Permite restauración total para el reinicio de arena.
    /// </summary>
    [DisallowMultipleComponent]
    public class DestructibleStructure : MonoBehaviour
    {
        [Header("--- Estado Intacto ---")]
        [Tooltip("Objeto raíz o malla visual que representa la estructura completa e intacta.")]
        [SerializeField] private GameObject intactVisualRoot;

        [Tooltip("Colisionador principal mientras la estructura está intacta.")]
        [SerializeField] private Collider intactCollider;

        [Tooltip("Puntos de salud de la estructura.")]
        [SerializeField] private float maxHealth = 60f;

        [Header("--- Estado Fracturado ---")]
        [Tooltip("Contenedor que almacena los fragmentos pre-fracturados con Rigidbodies.")]
        [SerializeField] private GameObject fracturedRoot;

        [Tooltip("Fuerza explosiva aplicada a los fragmentos al romperse.")]
        [SerializeField] private float fractureExplosionForce = 450f;

        [Tooltip("Radio de dispersión de la fractura.")]
        [SerializeField] private float fractureExplosionRadius = 5.0f;

        private float _currentHealth;
        private bool _isFractured = false;

        private readonly List<Rigidbody> _debrisRigidbodies = new List<Rigidbody>();
        private readonly List<Vector3> _initialPiecePositions = new List<Vector3>();
        private readonly List<Quaternion> _initialPieceRotations = new List<Quaternion>();

        public bool IsFractured => _isFractured;
        public float CurrentHealth => _currentHealth;

        public event Action<Vector3> OnStructureFractured;

        private void Awake()
        {
            if (intactVisualRoot == null)
            {
                var t = transform.Find("Intact_Visual");
                if (t != null) intactVisualRoot = t.gameObject;
            }
            if (intactCollider == null && intactVisualRoot != null)
            {
                intactCollider = intactVisualRoot.GetComponentInChildren<Collider>();
            }
            if (fracturedRoot == null)
            {
                var t = transform.Find("Fractured_Pieces");
                if (t != null) fracturedRoot = t.gameObject;
            }

            _currentHealth = maxHealth;
            CacheDebrisPieces();
            ResetStructure();
        }

        public void Initialize(GameObject intactVisual, Collider intactCol, GameObject fractured)
        {
            intactVisualRoot = intactVisual;
            intactCollider = intactCol;
            fracturedRoot = fractured;
            _debrisRigidbodies.Clear();
            _initialPiecePositions.Clear();
            _initialPieceRotations.Clear();
            CacheDebrisPieces();
            ResetStructure();
        }

        private void CacheDebrisPieces()
        {
            if (fracturedRoot != null)
            {
                var rbs = fracturedRoot.GetComponentsInChildren<Rigidbody>(true);
                foreach (var rb in rbs)
                {
                    _debrisRigidbodies.Add(rb);
                    _initialPiecePositions.Add(rb.transform.localPosition);
                    _initialPieceRotations.Add(rb.transform.localRotation);

                    // Asegurar que cada trozo pueda ser un objetivo válido para el Boogie-Woogie
                    if (!rb.TryGetComponent<TargetableEntity>(out var targetable))
                    {
                        var t = rb.gameObject.AddComponent<TargetableEntity>();
                    }
                }
            }
        }

        /// <summary>
        /// Aplica daño a la estructura (desde golpes directos, proyectiles de escombros o colisiones de enemigos lanzados).
        /// </summary>
        public void ApplyDamage(float damage, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (_isFractured) return;

            _currentHealth -= damage;
            if (_currentHealth <= 0f)
            {
                Fracture(hitPoint, hitDirection);
            }
        }

        /// <summary>
        /// Divide la estructura en fragmentos físicos independientes.
        /// </summary>
        public void Fracture(Vector3 impactPoint, Vector3 impactDirection)
        {
            if (_isFractured) return;

            _isFractured = true;

            // Desactivar representación unificada sólida
            if (intactVisualRoot != null) intactVisualRoot.SetActive(false);
            if (intactCollider != null) intactCollider.enabled = false;

            // Activar fragmentos con físicas
            if (fracturedRoot != null)
            {
                fracturedRoot.SetActive(true);

                foreach (var rb in _debrisRigidbodies)
                {
                    rb.isKinematic = false;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;

                    // Impulso direccional + explosión radial
                    rb.AddExplosionForce(fractureExplosionForce, impactPoint, fractureExplosionRadius, 1.2f, ForceMode.Impulse);
                    rb.AddForce(impactDirection.normalized * (fractureExplosionForce * 0.5f), ForceMode.Impulse);
                }
            }

            OnStructureFractured?.Invoke(impactPoint);
        }

        /// <summary>
        /// Restaura la estructura a su estado original (llamado cada 5 minutos por el ArenaResetManager).
        /// </summary>
        public void ResetStructure()
        {
            _currentHealth = maxHealth;
            _isFractured = false;

            if (intactVisualRoot != null) intactVisualRoot.SetActive(true);
            if (intactCollider != null) intactCollider.enabled = true;

            if (fracturedRoot != null)
            {
                for (int i = 0; i < _debrisRigidbodies.Count; i++)
                {
                    var rb = _debrisRigidbodies[i];
                    rb.isKinematic = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.transform.localPosition = _initialPiecePositions[i];
                    rb.transform.localRotation = _initialPieceRotations[i];
                }

                fracturedRoot.SetActive(false);
            }
        }
    }
}
