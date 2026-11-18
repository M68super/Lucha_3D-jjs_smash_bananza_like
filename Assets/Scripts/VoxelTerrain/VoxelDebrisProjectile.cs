using UnityEngine;
using Lucha3D.Movement;
using Lucha3D.Abilities;
using Lucha3D.Core;
using Lucha3D.Combat;

namespace Lucha3D.VoxelTerrain
{
    /// <summary>
    /// Representa un trozo de escombro rocoso arrancado del suelo voxelizado.
    /// Puede ser sostenido por el jugador o lanzado a gran velocidad con físicas activas,
    /// colisionando con enemigos o estructuras destructibles.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(TargetableEntity))]
    [DisallowMultipleComponent]
    public class VoxelDebrisProjectile : MonoBehaviour
    {
        [Header("--- Parámetros del Proyectil ---")]
        [Tooltip("Daño base infligido al impactar.")]
        [SerializeField] private float baseDamage = 35f;

        [Tooltip("Fuerza de impulso/knockback al colisionar.")]
        [SerializeField] private float knockbackForce = 22f;

        [Tooltip("Radio de explosión/impacto al estrellarse.")]
        [SerializeField] private float impactRadius = 2.5f;

        [Tooltip("Capas con las que colisiona el escombro.")]
        [SerializeField] private LayerMask hitLayers = ~0;

        private Rigidbody _rigidbody;
        private Collider _collider;
        private GameObject _owner;
        private bool _isThrown = false;
        private bool _hasImpacted = false;

        public bool IsThrown => _isThrown;
        public Rigidbody RigidbodyComponent => _rigidbody;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
        }

        /// <summary>
        /// Asigna el escombro como objeto en mano del jugador.
        /// </summary>
        public void HoldInHand(Transform handSocket, GameObject owner)
        {
            _owner = owner;
            _isThrown = false;
            _hasImpacted = false;

            _rigidbody.isKinematic = true;
            _collider.enabled = false;

            transform.SetParent(handSocket);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Lanza el escombro con una velocidad y dirección balística.
        /// </summary>
        public void Launch(Vector3 direction, float throwSpeed, float damageMultiplier = 1.0f)
        {
            transform.SetParent(null);
            _collider.enabled = true;
            _rigidbody.isKinematic = false;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;

            _isThrown = true;
            baseDamage *= damageMultiplier;

            // Impulso balístico inicial
            _rigidbody.linearVelocity = direction.normalized * throwSpeed;
            _rigidbody.AddTorque(Random.insideUnitSphere * 15f, ForceMode.Impulse);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_isThrown || _hasImpacted) return;

            // Ignorar choque inicial con el lanzador
            if (_owner != null && (collision.gameObject == _owner || collision.transform.IsChildOf(_owner.transform)))
            {
                return;
            }

            _hasImpacted = true;
            ExplodeImpact(collision.contacts[0].point);
        }

        private void ExplodeImpact(Vector3 contactPoint)
        {
            Collider[] hits = Physics.OverlapSphere(contactPoint, impactRadius, hitLayers);

            foreach (var hit in hits)
            {
                if (_owner != null && (hit.gameObject == _owner || hit.transform.IsChildOf(_owner.transform)))
                {
                    continue;
                }

                Vector3 knockDir = (hit.transform.position - contactPoint).normalized + Vector3.up * 0.4f;

                // 1. Maniquí de entrenamiento
                var dummy = hit.GetComponentInParent<TrainingDummy>();
                if (dummy != null)
                {
                    dummy.TakeHit(baseDamage, knockDir * knockbackForce);
                    continue;
                }

                // 2. Salud
                var health = hit.GetComponentInParent<HealthSystem>();
                if (health != null)
                {
                    health.TakeDamage(baseDamage);
                }

                // 3. Knockback a personajes
                if (hit.TryGetComponent<PlayerLocomotion>(out var locomotion))
                {
                    locomotion.ApplyKnockback(knockDir * knockbackForce);
                }

                // 4. Físicas a otros Rigidbodies
                if (hit.TryGetComponent<Rigidbody>(out var hitRb) && hitRb != _rigidbody)
                {
                    hitRb.AddExplosionForce(knockbackForce * 100f, contactPoint, impactRadius);
                }
            }

            // Destruir o reciclar el escombro tras el impacto
            Destroy(gameObject, 0.1f);
        }
    }
}
