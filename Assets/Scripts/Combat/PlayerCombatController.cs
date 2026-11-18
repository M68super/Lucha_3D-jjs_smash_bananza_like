using System;
using UnityEngine;
using Lucha3D.Core;
using Lucha3D.Movement;

namespace Lucha3D.Combat
{
    public enum AttackDirection
    {
        NeutralOrForward,
        Side,
        Back,
        Up,
        Down
    }

    public enum AttackType
    {
        Normal,
        Charged
    }

    /// <summary>
    /// Controlador integral de combate inspirado en Super Smash Bros.
    /// Gestiona ataques normales y cargados en 5 direcciones, progresión matemática de daño/hitbox,
    /// slow-fall aéreo y visualización en tiempo real del hitbox (esfera transparente en Play Mode).
    /// </summary>
    [RequireComponent(typeof(PlayerLocomotion))]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(StaminaSystem))]
    [DisallowMultipleComponent]
    public class PlayerCombatController : MonoBehaviour
    {
        [Header("--- Referencias ---")]
        [SerializeField] private PlayerLocomotion locomotion;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private StaminaSystem staminaSystem;

        [Header("--- Costes y Valores Base ---")]
        [SerializeField] private float normalAttackCost   = 10f;
        [SerializeField] private float chargeBaseCost     = 15f;
        [SerializeField] private float chargeTickCost     = 5f;
        [SerializeField] private float baseChargedDamage  = 20f;
        [SerializeField] private float baseChargedRadius  = 2.0f;
        [SerializeField] private float chargeThreshold    = 0.25f;
        [SerializeField] private float normalAttackDuration = 0.35f;

        [Header("--- Configuración de Hitbox ---")]
        [SerializeField] private Vector3 hitboxCenterOffset = new Vector3(0f, 1f, 1.2f);
        [SerializeField] private LayerMask hitLayers = ~0;

        // Estado interno
        private bool _isCharging  = false;
        private bool _isAttacking = false;
        private float _attackHoldTimer      = 0f;
        private float _chargeDuration       = 0f;
        private float _chargeTickTimer      = 0f;
        private int   _chargeTicksCompleted = 0;
        private AttackDirection _currentAttackDirection;

        // Visualización de hitbox en tiempo de ejecución
        private GameObject  _hitboxVisualObj;
        private Renderer    _hitboxRenderer;

        #region Eventos Públicos
        public event Action<AttackDirection, bool>            OnChargeStarted;
        public event Action<float, float, float>              OnChargeUpdated;
        public event Action<AttackDirection, bool>            OnNormalAttackExecuted;
        public event Action<AttackDirection, bool, float, float> OnChargedAttackExecuted;
        public event Action<Vector3, float, float>            OnGroundRipTriggered;
        #endregion

        #region Propiedades
        public bool  IsCharging              => _isCharging;
        public bool  IsAttacking             => _isAttacking;
        public float CurrentChargeDuration   => _chargeDuration;
        public float CurrentCalculatedDamage => CalculateDamage(_chargeTicksCompleted);
        public float CurrentCalculatedRadius => CalculateHitboxRadius(_chargeTicksCompleted);
        #endregion

        private void Reset()
        {
            locomotion   = GetComponent<PlayerLocomotion>();
            inputReader  = GetComponent<PlayerInputReader>();
            staminaSystem = GetComponent<StaminaSystem>();
        }

        private void Awake()
        {
            if (locomotion    == null) locomotion    = GetComponent<PlayerLocomotion>();
            if (inputReader   == null) inputReader   = GetComponent<PlayerInputReader>();
            if (staminaSystem == null) staminaSystem = GetComponent<StaminaSystem>();

            CreateHitboxVisual();
        }

        private void OnEnable()
        {
            if (inputReader != null)
            {
                inputReader.OnAttackPressed  += HandleAttackPressed;
                inputReader.OnAttackReleased += HandleAttackReleased;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.OnAttackPressed  -= HandleAttackPressed;
                inputReader.OnAttackReleased -= HandleAttackReleased;
            }
            if (_isCharging) CancelCharge();
        }

        private void Update()
        {
            HandleAttackUpdate();
            UpdateHitboxVisual();
        }

        #region Visualización de Hitbox en Play Mode
        /// <summary>
        /// Crea una esfera hija transparente para ver el hitbox durante el Play Mode.
        /// </summary>
        private void CreateHitboxVisual()
        {
            _hitboxVisualObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _hitboxVisualObj.name = "HitboxVisual";
            _hitboxVisualObj.transform.SetParent(transform, false);

            // Eliminar el collider para que la esfera visual no interfiera con la física
            Destroy(_hitboxVisualObj.GetComponent<Collider>());

            // Material transparente visible en Play Mode
            var mat = new Material(Shader.Find("Sprites/Default"));
            if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
            {
                // Fallback para URP
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", new Color(1f, 0.3f, 0f, 0.3f));
            }
            else
            {
                mat.color = new Color(1f, 0.3f, 0f, 0.3f);
            }
            mat.renderQueue = 3000; // Transparente
            _hitboxRenderer = _hitboxVisualObj.GetComponent<Renderer>();
            _hitboxRenderer.material = mat;
            _hitboxRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _hitboxRenderer.receiveShadows    = false;

            _hitboxVisualObj.SetActive(false);
        }

        private void UpdateHitboxVisual()
        {
            if (_hitboxVisualObj == null) return;

            bool shouldShow = _isCharging || _isAttacking;
            _hitboxVisualObj.SetActive(shouldShow);

            if (shouldShow)
            {
                float radius = _isCharging ? CurrentCalculatedRadius : baseChargedRadius;
                Vector3 center = transform.position + transform.TransformDirection(hitboxCenterOffset);
                _hitboxVisualObj.transform.position   = center;
                _hitboxVisualObj.transform.localScale = Vector3.one * radius * 2f;

                // Color: naranja al cargar, amarillo en ataque normal
                if (_hitboxRenderer != null)
                {
                    Color c = _isCharging
                        ? new Color(1f, 0.2f, 0f, 0.35f)
                        : new Color(1f, 0.9f, 0f, 0.3f);

                    if (_hitboxRenderer.material.HasProperty("_BaseColor"))
                        _hitboxRenderer.material.SetColor("_BaseColor", c);
                    else if (_hitboxRenderer.material.HasProperty("_Color"))
                        _hitboxRenderer.material.SetColor("_Color", c);
                    else
                        _hitboxRenderer.material.color = c;
                }
            }
        }
        #endregion

        #region Máquina de Estados de Ataque
        private void HandleAttackPressed()
        {
            if (locomotion.AreControlsFrozen || _isAttacking) return;
            _currentAttackDirection = DetermineAttackDirection();
            _attackHoldTimer = 0f;
        }

        private void HandleAttackReleased()
        {
            if (locomotion.AreControlsFrozen) return;

            if (_isCharging)
                ReleaseChargedAttack();
            else if (_attackHoldTimer < chargeThreshold && !_isAttacking)
                ExecuteNormalAttack();

            _attackHoldTimer = 0f;
        }

        private void HandleAttackUpdate()
        {
            if (locomotion.AreControlsFrozen)
            {
                if (_isCharging) CancelCharge();
                return;
            }

            if (inputReader.IsAttackHeld && !_isAttacking)
            {
                _attackHoldTimer += Time.deltaTime;

                if (!_isCharging && _attackHoldTimer >= chargeThreshold)
                    StartCharging();

                if (_isCharging)
                {
                    _chargeDuration    += Time.deltaTime;
                    _chargeTickTimer   += Time.deltaTime;

                    while (_chargeTickTimer >= 0.25f)
                    {
                        _chargeTickTimer -= 0.25f;
                        if (staminaSystem.TryConsumeStamina(chargeTickCost))
                            _chargeTicksCompleted++;
                        else
                        {
                            ReleaseChargedAttack();
                            return;
                        }
                    }

                    OnChargeUpdated?.Invoke(_chargeDuration, CurrentCalculatedDamage, CurrentCalculatedRadius);
                }
            }
        }

        private void StartCharging()
        {
            if (!staminaSystem.TryConsumeStamina(chargeBaseCost))
            {
                _attackHoldTimer = 0f;
                return;
            }

            _isCharging = true;
            _chargeDuration = _chargeTickTimer = 0f;
            _chargeTicksCompleted = 0;

            staminaSystem.AddRegenBlocker();
            if (!locomotion.IsGrounded) locomotion.SetSlowFallGravity(true);

            OnChargeStarted?.Invoke(_currentAttackDirection, !locomotion.IsGrounded);
        }

        private void ReleaseChargedAttack()
        {
            if (!_isCharging) return;

            _isCharging = false;
            locomotion.SetSlowFallGravity(false);

            float finalDamage = CalculateDamage(_chargeTicksCompleted);
            float finalRadius = CalculateHitboxRadius(_chargeTicksCompleted);
            bool  isAerial   = !locomotion.IsGrounded;

            staminaSystem.RemoveRegenBlocker();

            if (inputReader.IsCrouchHeld && _currentAttackDirection == AttackDirection.Down && locomotion.IsGrounded)
            {
                Vector3 groundContact = transform.position + transform.forward * 1.0f;
                OnGroundRipTriggered?.Invoke(groundContact, _chargeDuration, finalDamage);
            }

            ApplyHitboxDamage(finalDamage, finalRadius, isCharged: true);
            OnChargedAttackExecuted?.Invoke(_currentAttackDirection, isAerial, finalDamage, finalRadius);
        }

        private void CancelCharge()
        {
            _isCharging = false;
            locomotion.SetSlowFallGravity(false);
            staminaSystem.RemoveRegenBlocker();
        }

        private void ExecuteNormalAttack()
        {
            if (!staminaSystem.TryConsumeStamina(normalAttackCost)) return;

            _isAttacking = true;
            bool isAerial = !locomotion.IsGrounded;

            staminaSystem.AddRegenBlocker();
            ApplyHitboxDamage(baseChargedDamage, baseChargedRadius, isCharged: false);
            OnNormalAttackExecuted?.Invoke(_currentAttackDirection, isAerial);

            Invoke(nameof(FinishNormalAttack), normalAttackDuration);
        }

        private void FinishNormalAttack()
        {
            _isAttacking = false;
            staminaSystem.RemoveRegenBlocker();
        }
        #endregion

        #region Matemáticas de Progresión de Carga
        public float CalculateDamage(int ticks)
        {
            float d = baseChargedDamage;
            if (ticks <= 0) return d;
            d += Mathf.Min(ticks, 3) * 5f;
            d += Mathf.Clamp(ticks - 3, 0, 2) * 4f;
            d += Mathf.Clamp(ticks - 5, 0, 2) * 3f;
            d += Mathf.Clamp(ticks - 7, 0, 2) * 2f;
            d += Mathf.Max(0, ticks - 9) * 1f;
            return d;
        }

        public float CalculateHitboxRadius(int ticks)
        {
            float r = baseChargedRadius;
            if (ticks <= 0) return r;
            r += Mathf.Min(ticks, 3) * 0.5f;
            r += Mathf.Clamp(ticks - 3, 0, 2) * 0.4f;
            r += Mathf.Clamp(ticks - 5, 0, 2) * 0.3f;
            r += Mathf.Clamp(ticks - 7, 0, 2) * 0.2f;
            r += Mathf.Clamp(ticks - 9, 0, 2) * 0.1f;
            return Mathf.Min(r, 5.5f);
        }
        #endregion

        #region Determinación Direccional (Smash-Style)
        public AttackDirection DetermineAttackDirection()
        {
            Vector2 input = inputReader.MoveInput;
            if (input.y >  0.55f) return AttackDirection.Up;
            if (input.y < -0.55f || inputReader.IsCrouchHeld) return AttackDirection.Down;
            if (Mathf.Abs(input.x) > 0.55f) return AttackDirection.Side;
            return AttackDirection.NeutralOrForward;
        }
        #endregion

        #region Detección de Colisiones e Impacto
        private void ApplyHitboxDamage(float damage, float radius, bool isCharged)
        {
            Vector3 center = transform.position + transform.TransformDirection(hitboxCenterOffset);
            Collider[] hits = Physics.OverlapSphere(center, radius, hitLayers);

            foreach (var hit in hits)
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

                Vector3 dir   = (hit.transform.position - transform.position).normalized;
                dir.y = 0.4f;
                float force   = (damage / 20f) * (isCharged ? 18f : 10f);

                var dummy = hit.GetComponentInParent<TrainingDummy>();
                if (dummy != null)
                {
                    dummy.TakeHit(damage, dir * force);
                    continue;
                }

                var health = hit.GetComponentInParent<HealthSystem>();
                if (health != null) health.TakeDamage(damage);

                if (hit.TryGetComponent<PlayerLocomotion>(out var tLoc))
                    tLoc.ApplyKnockback(dir * force);

                if (hit.TryGetComponent<Rigidbody>(out var rb))
                {
                    Vector3 pushDir = (hit.transform.position - center).normalized + Vector3.up * 0.3f;
                    rb.AddForce(pushDir * damage * 15f, ForceMode.Impulse);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isCharging ? Color.red : Color.yellow;
            Vector3 center = transform.position + transform.TransformDirection(hitboxCenterOffset);
            float   radius = _isCharging ? CurrentCalculatedRadius : baseChargedRadius;
            Gizmos.DrawWireSphere(center, radius);
        }
        #endregion
    }
}
