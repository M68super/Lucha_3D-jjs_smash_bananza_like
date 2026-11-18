using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Lucha3D.Core;
using Lucha3D.Movement;
using Lucha3D.Combat;

namespace Lucha3D.Abilities
{
    // ─────────────────────────────────────────────────────────────────────────────
    //  Tipos de inputs que se pueden grabar en el buffer de la Técnica
    // ─────────────────────────────────────────────────────────────────────────────
    public enum BufferedInputType
    {
        Attack,   // E durante la ventana
        Sprint,   // Q durante la ventana (Q se libera para sprint una vez lanzada la técnica)
        Crouch,   // F durante la ventana
        Move      // Flechas de dirección
    }

    [System.Serializable]
    public struct BufferedInput
    {
        public float            timestamp;  // Tiempo desde que comenzó la ventana de grabación
        public BufferedInputType type;
        public Vector2          moveDir;    // Solo para Move; ignorado en el resto
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Técnica de Proyección — Naobito/Naoya Zen'in a 24 FPS
    // ─────────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Implementa la "Técnica de Proyección" (24 FPS).
    ///
    /// FASES:
    ///   1. Preparación (0.5s fijo): La cámara se fija en el enemigo más cercano.
    ///      El jugador PUEDE moverse con WASD; los controles NO se congelan.
    ///      El botón de ataque normal cambia de Click Izquierdo → E (sólo durante esta fase).
    ///
    ///   2. Ventana de grabación (0.25s–1.0s según Fuerza):
    ///      La pantalla se oscurece levemente como señal.
    ///      WASD, Ctrl, Shift siguen funcionando con normalidad (movimiento real).
    ///      Las flechas de dirección, E, Q y F graban inputs en el buffer:
    ///        - Flechas → graba movimiento
    ///        - E       → graba ataque
    ///        - Q       → graba sprint
    ///        - F       → graba agacharse
    ///
    ///   3. Ráfaga (1/24s ≈ 0.04167s): Se reproducen todos los inputs grabados
    ///      comprimidos al tiempo de ráfaga. Velocidad × (windowDuration / burstDuration).
    ///
    /// TECLA DE ACTIVACIÓN: Q (activar técnica)
    /// </summary>
    [RequireComponent(typeof(PlayerLocomotion))]
    [RequireComponent(typeof(StaminaSystem))]
    [DisallowMultipleComponent]
    public class ProjectionTechnique : MonoBehaviour
    {
        #region Constantes fijas de la técnica
        public const float INITIAL_PREPARATION_MARGIN = 0.5f;        // Fase 1: 0.5s fijos
        public const float BURST_EXECUTION_DURATION   = 1f / 24f;    // Fase 3: exactamente 1/24s
        public const float BASE_ACTIVATION_COST       = 50f;         // Coste de estamina base
        #endregion

        [Header("--- Referencias ---")]
        [SerializeField] private PlayerLocomotion locomotion;
        [SerializeField] private StaminaSystem    staminaSystem;
        [SerializeField] private CameraFollow     cameraFollow;

        [Header("--- Ventana de Inputs según Fuerza ---")]
        [Tooltip("Duración mínima de la ventana a Fuerza x1 (0.25s).")]
        [SerializeField] private float minInputWindow = 0.25f;
        [Tooltip("Duración máxima de la ventana a Fuerza x4 (1.0s).")]
        [SerializeField] private float maxInputWindow = 1.0f;

        [Header("--- Búsqueda de Objetivo ---")]
        [SerializeField] private float    searchRange  = 25f;
        [SerializeField] private LayerMask enemyLayers = ~0;

        [Header("--- Daño de Ráfaga ---")]
        [Tooltip("Daño base por cada input de Ataque grabado.")]
        [SerializeField] private float attackInputDamage = 12f;

        // Estado
        private bool      _isTechniqueActive  = false;
        private bool      _isPrepPhase         = false; // Fase 1 activa
        private bool      _isInputWindowOpen   = false; // Fase 2 activa
        private bool      _isBurstPhase        = false; // Fase 3 activa
        private Transform _lockedTarget;
        private Coroutine _techniqueRoutine;

        // Buffer de inputs grabados
        private readonly List<BufferedInput> _inputBuffer = new();
        private float _recordingStartTime;

        // Overlay de oscurecimiento (Panel OnGUI)
        private float _overlayAlpha = 0f;

        #region Eventos
        public event Action<Transform> OnTechniqueStarted;
        public event Action<float>     OnInputWindowOpened;
        public event Action<int>       OnBurstExecutionStarted;
        public event Action            OnTechniqueCompleted;
        #endregion

        #region Propiedades
        public bool      IsTechniqueActive => _isTechniqueActive;
        public bool      IsInputWindowOpen => _isInputWindowOpen;
        public Transform LockedTarget      => _lockedTarget;
        public int       BufferedInputCount => _inputBuffer.Count;
        #endregion

        private void Reset()
        {
            locomotion    = GetComponent<PlayerLocomotion>();
            staminaSystem = GetComponent<StaminaSystem>();
            if (cameraFollow == null) cameraFollow = FindFirstObjectByType<CameraFollow>();
        }

        private void Awake()
        {
            if (locomotion    == null) locomotion    = GetComponent<PlayerLocomotion>();
            if (staminaSystem == null) staminaSystem = GetComponent<StaminaSystem>();
            if (cameraFollow  == null) cameraFollow  = FindFirstObjectByType<CameraFollow>();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // Q activa la técnica (si no está activa)
            if (kb.qKey.wasPressedThisFrame && !_isTechniqueActive)
            {
                TryActivate();
                return;
            }

            // Durante la ventana de grabación, capturar inputs del buffer
            if (_isInputWindowOpen)
                CaptureBufferInputs(kb);
        }

        #region Activación
        public bool TryActivate()
        {
            if (_isTechniqueActive) return false;
            if (!staminaSystem.TryConsumeStamina(BASE_ACTIVATION_COST)) return false;

            _lockedTarget = FindNearestTarget();

            if (_techniqueRoutine != null) StopCoroutine(_techniqueRoutine);
            _techniqueRoutine = StartCoroutine(TechniqueRoutine());
            return true;
        }

        public float CalculateInputWindowDuration()
        {
            float f = staminaSystem != null ? staminaSystem.CurrentStrengthIndex : 1f;
            return Mathf.Lerp(minInputWindow, maxInputWindow, Mathf.Clamp01((f - 1f) / 3f));
        }
        #endregion

        #region Rutina Principal (3 Fases)
        private IEnumerator TechniqueRoutine()
        {
            _isTechniqueActive = true;
            _inputBuffer.Clear();
            staminaSystem.AddRegenBlocker();

            // ── FASE 1: Preparación 0.5s ─────────────────────────────────────
            // El jugador se puede seguir moviendo (controles NO congelados).
            // La cámara se fija en el objetivo más cercano.
            _isPrepPhase = true;

            if (cameraFollow != null && _lockedTarget != null)
                cameraFollow.SetLockedTarget(_lockedTarget);

            OnTechniqueStarted?.Invoke(_lockedTarget);

            float prep = 0f;
            while (prep < INITIAL_PREPARATION_MARGIN)
            {
                prep += Time.deltaTime;
                yield return null;
            }

            _isPrepPhase = false;

            // ── FASE 2: Ventana de Grabación ─────────────────────────────────
            _isInputWindowOpen    = true;
            _recordingStartTime   = Time.time;
            float windowDuration  = CalculateInputWindowDuration();

            // Oscurecer pantalla gradualmente (el overlay se dibuja en OnGUI)
            _overlayAlpha = 0.3f;

            OnInputWindowOpened?.Invoke(windowDuration);

            float windowTimer = 0f;
            while (windowTimer < windowDuration)
            {
                windowTimer += Time.deltaTime;
                yield return null;
            }

            _isInputWindowOpen = false;
            _overlayAlpha = 0f;

            // ── FASE 3: Ráfaga 1/24s ─────────────────────────────────────────
            _isBurstPhase = true;
            OnBurstExecutionStarted?.Invoke(_inputBuffer.Count);
            yield return StartCoroutine(BurstRoutine(windowDuration));
            _isBurstPhase = false;

            // Finalización
            if (cameraFollow != null) cameraFollow.ClearLockedTarget();
            staminaSystem.RemoveRegenBlocker();
            _isTechniqueActive = false;
            _lockedTarget      = null;

            OnTechniqueCompleted?.Invoke();
        }
        #endregion

        #region Captura de Inputs en la Ventana (Buffer)
        private void CaptureBufferInputs(Keyboard kb)
        {
            float t = Time.time - _recordingStartTime;

            // Flechas → Movimiento grabado
            Vector2 arrowDir = Vector2.zero;
            if (kb.upArrowKey.isPressed)    arrowDir.y += 1f;
            if (kb.downArrowKey.isPressed)  arrowDir.y -= 1f;
            if (kb.rightArrowKey.isPressed) arrowDir.x += 1f;
            if (kb.leftArrowKey.isPressed)  arrowDir.x -= 1f;

            if (arrowDir.sqrMagnitude > 0.001f)
            {
                _inputBuffer.Add(new BufferedInput
                {
                    timestamp = t,
                    type      = BufferedInputType.Move,
                    moveDir   = arrowDir.normalized
                });
            }

            // E → Ataque grabado
            if (kb.eKey.wasPressedThisFrame)
            {
                _inputBuffer.Add(new BufferedInput { timestamp = t, type = BufferedInputType.Attack });
            }

            // Q → Sprint grabado (Q ya fue consumida al activar la técnica, ahora está libre)
            if (kb.qKey.wasPressedThisFrame)
            {
                _inputBuffer.Add(new BufferedInput { timestamp = t, type = BufferedInputType.Sprint });
            }

            // F → Agacharse grabado
            if (kb.fKey.wasPressedThisFrame)
            {
                _inputBuffer.Add(new BufferedInput { timestamp = t, type = BufferedInputType.Crouch });
            }
        }
        #endregion

        #region Ráfaga — Reproducción del Buffer comprimida
        private IEnumerator BurstRoutine(float windowDuration)
        {
            if (_inputBuffer.Count == 0)
            {
                // Sin inputs grabados → golpe base mínimo al objetivo
                ApplyBurstAttack(attackInputDamage);
                yield break;
            }

            // speedMultiplier = windowDuration / burstDuration
            // Ejemplo: 0.25s / (1/24s) ≈ ×6 | 1.0s / (1/24s) ≈ ×24
            float speedMultiplier = windowDuration / BURST_EXECUTION_DURATION;
            float burstStart      = Time.time;

            int attackCount = 0;

            foreach (var input in _inputBuffer)
            {
                // Tiempo en el que se debe ejecutar este input dentro de la ráfaga
                float scheduledTime = burstStart + (input.timestamp / speedMultiplier);

                // Esperar hasta que llegue ese momento dentro del frame
                while (Time.time < scheduledTime)
                    yield return null;

                // Ejecutar el input
                switch (input.type)
                {
                    case BufferedInputType.Attack:
                        attackCount++;
                        ApplyBurstAttack(attackInputDamage);
                        break;

                    case BufferedInputType.Move:
                        // Aplica un pequeño impulso de movimiento en la dirección grabada
                        if (locomotion != null)
                        {
                            Vector3 worldDir = transform.TransformDirection(new Vector3(input.moveDir.x, 0f, input.moveDir.y));
                            locomotion.ApplyKnockback(worldDir * 2f); // impulso suave
                        }
                        break;

                    case BufferedInputType.Sprint:
                        // Durante la ráfaga el sprint se simula como un pequeño dash hacia adelante
                        if (locomotion != null)
                            locomotion.ApplyKnockback(transform.forward * 3f);
                        break;

                    case BufferedInputType.Crouch:
                        // Agacharse en la ráfaga = movimiento hacia abajo breve
                        if (locomotion != null)
                            locomotion.ApplyKnockback(Vector3.down * 1.5f);
                        break;
                }
            }

            // Si no hubo ningún ataque entre los inputs pero sí otros, atacar una vez de base
            if (attackCount == 0)
                ApplyBurstAttack(attackInputDamage);

            // Garantizar que la ráfaga no dure más de 1/24s real
            float elapsed = Time.time - burstStart;
            if (elapsed < BURST_EXECUTION_DURATION)
                yield return new WaitForSeconds(BURST_EXECUTION_DURATION - elapsed);
        }

        /// <summary>
        /// Aplica daño al objetivo fijado durante la ráfaga.
        /// </summary>
        private void ApplyBurstAttack(float damage)
        {
            if (_lockedTarget == null) return;

            Vector3 knockVec = ((_lockedTarget.position - transform.position).normalized) * (damage * 0.4f) + Vector3.up * 4f;

            var dummy = _lockedTarget.GetComponentInParent<TrainingDummy>();
            if (dummy != null)
            {
                dummy.TakeHit(damage, knockVec);
            }
            else
            {
                var health = _lockedTarget.GetComponentInParent<HealthSystem>();
                health?.TakeDamage(damage);

                if (_lockedTarget.TryGetComponent<PlayerLocomotion>(out var tLoc))
                    tLoc.ApplyKnockback(knockVec);
                else if (_lockedTarget.TryGetComponent<Rigidbody>(out var rb))
                    rb.AddForce(knockVec * 25f, ForceMode.Impulse);
            }
        }
        #endregion

        #region Búsqueda de Objetivo
        private Transform FindNearestTarget()
        {
            Collider[] cols = Physics.OverlapSphere(transform.position, searchRange, enemyLayers);
            Transform best  = null;
            float minDist   = float.MaxValue;

            foreach (var col in cols)
            {
                if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

                // Priorizar objetos con TrainingDummy o TargetableEntity
                if (col.GetComponentInParent<TrainingDummy>() == null &&
                    col.GetComponentInParent<TargetableEntity>() == null)
                    continue;

                float d = Vector3.Distance(transform.position, col.transform.position);
                if (d < minDist) { minDist = d; best = col.transform; }
            }

            return best;
        }
        #endregion

        #region Overlay OnGUI (oscurecimiento de pantalla durante la ventana)
        private void OnGUI()
        {
            if (!_isInputWindowOpen || _overlayAlpha <= 0f) return;

            GUI.color = new Color(0f, 0f, 0.08f, _overlayAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Indicador de texto en la esquina superior
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter
            };
            style.normal.textColor = new Color(0.6f, 0.9f, 1f);

            GUI.Label(new Rect(0, 20, Screen.width, 30),
                "[ TÉCNICA DE PROYECCIÓN · INSERTANDO INPUTS ]   " +
                $"Buffer: {_inputBuffer.Count} | " +
                $"↑↓←→ Mover   E Atacar   Q Sprint   F Agacharse",
                style);
        }
        #endregion
    }
}
