using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Lucha3D.Core;
using Lucha3D.Movement;

namespace Lucha3D.Abilities
{
    /// <summary>
    /// Implementa la habilidad "Boogie-Woogie" (Aoi Todo).
    /// Controles:
    ///   E  → Selecciona el objetivo en la mira (1º libre, luego 2º)
    ///   R  → Selecciona al propio jugador (1º libre, luego 2º)
    ///   Q  → Desmarca el último objetivo (2 → 1 → nada)
    ///   F  → ¡Ejecutar intercambio de posiciones!
    /// El objetivo que está en la "mira" se destaca visualmente con un brillo/color amarillo.
    /// </summary>
    [RequireComponent(typeof(StaminaSystem))]
    [DisallowMultipleComponent]
    public class BoogieWoogieAbility : MonoBehaviour
    {
        [Header("--- Dependencias ---")]
        [SerializeField] private StaminaSystem staminaSystem;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private TargetableEntity selfTargetable;

        [Header("--- Parámetros de Habilidad ---")]
        [SerializeField] private float swapStaminaCost   = 25f;
        [SerializeField] private float baseRange         = 20.0f;
        [SerializeField] private float maxRange          = 80.0f;
        [Range(0.05f, 0.5f)]
        [SerializeField] private float screenCenterTolerance = 0.35f;
        [SerializeField] private LayerMask targetSearchLayers = ~0;

        [Header("--- Controles ---")]
        [SerializeField] private Key selectTargetKey  = Key.E;
        [SerializeField] private Key undoLastTargetKey = Key.Q;
        [SerializeField] private Key selectSelfKey    = Key.R;
        [SerializeField] private Key activateSwapKey  = Key.F;

        // Ranuras
        private TargetableEntity _target1;
        private TargetableEntity _target2;
        private TargetableEntity _currentlyHoveredTarget;
        private TargetableEntity _previousHoveredTarget;

        // Material original del objetivo en mira (para restaurar al salir)
        private Renderer[] _hoveredRenderers;
        private Color[]    _hoveredOriginalColors;

        #region Eventos
        public event Action<TargetableEntity>            OnHoveredTargetChanged;
        public event Action<TargetableEntity, TargetableEntity> OnMarksUpdated;
        public event Action<Vector3, Vector3>            OnSwapExecuted;
        public event Action                              OnMarksCleared;
        #endregion

        #region Propiedades
        public TargetableEntity Target1    => _target1;
        public TargetableEntity Target2    => _target2;
        public TargetableEntity SlotA      => _target1;
        public TargetableEntity SlotB      => _target2;
        public TargetableEntity HoveredTarget => _currentlyHoveredTarget;
        public float CurrentRange          => CalculateCurrentRange();
        public bool  CanSwap               => _target1 != null && _target2 != null &&
                                              _target1 != _target2 &&
                                              staminaSystem.HasEnoughStamina(swapStaminaCost);
        public int MarkedCount             => (_target1 != null ? 1 : 0) + (_target2 != null ? 1 : 0);
        #endregion

        private void Reset()
        {
            staminaSystem  = GetComponent<StaminaSystem>();
            selfTargetable = GetComponent<TargetableEntity>();
            if (playerCamera == null && Camera.main != null) playerCamera = Camera.main;
        }

        private void Awake()
        {
            if (staminaSystem  == null) staminaSystem  = GetComponent<StaminaSystem>();
            if (selfTargetable == null) selfTargetable = GetComponent<TargetableEntity>();
            if (playerCamera   == null && Camera.main != null) playerCamera = Camera.main;
        }

        private void OnDisable()
        {
            // Restaurar highlight al desactivar
            ClearHoverHighlight(_previousHoveredTarget);
            _previousHoveredTarget = null;
            _currentlyHoveredTarget = null;
        }

        private void Update()
        {
            UpdateHoveredTarget();
            HandleDirectKeyInputs();
        }

        #region Rango según Fuerza
        public float CalculateCurrentRange()
        {
            float f = staminaSystem != null ? staminaSystem.CurrentStrengthIndex : 1.0f;
            return Mathf.Lerp(baseRange, maxRange, Mathf.Clamp01((f - 1f) / 3f));
        }
        #endregion

        #region Rastreo y Highlight del Objetivo en Mira
        private void UpdateHoveredTarget()
        {
            if (playerCamera == null)
            {
                if (Camera.main != null) playerCamera = Camera.main;
                else return;
            }

            float maxR = CalculateCurrentRange();
            Collider[] colliders = Physics.OverlapSphere(transform.position, maxR, targetSearchLayers);

            TargetableEntity best = null;
            float bestDist = float.MaxValue;
            Vector2 screenCenter = new Vector2(0.5f, 0.5f);

            foreach (var col in colliders)
            {
                if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

                if (col.TryGetComponent<TargetableEntity>(out var candidate) &&
                    candidate.CanBeSwapped && candidate.gameObject.activeInHierarchy)
                {
                    Vector3 vp = playerCamera.WorldToViewportPoint(candidate.AimFocusPoint.position);
                    if (vp.z > 0f)
                    {
                        float d = Vector2.Distance(new Vector2(vp.x, vp.y), screenCenter);
                        if (d <= screenCenterTolerance && d < bestDist)
                        {
                            bestDist = d;
                            best     = candidate;
                        }
                    }
                }
            }

            if (_currentlyHoveredTarget != best)
            {
                // Quitar highlight al anterior
                ClearHoverHighlight(_currentlyHoveredTarget);

                _previousHoveredTarget  = _currentlyHoveredTarget;
                _currentlyHoveredTarget = best;

                // Aplicar highlight al nuevo
                ApplyHoverHighlight(_currentlyHoveredTarget);

                OnHoveredTargetChanged?.Invoke(_currentlyHoveredTarget);
            }
        }

        /// <summary>
        /// Aplica un brillo amarillo al objeto objetivo en la mira.
        /// </summary>
        private void ApplyHoverHighlight(TargetableEntity target)
        {
            if (target == null) return;

            _hoveredRenderers      = target.GetComponentsInChildren<Renderer>();
            _hoveredOriginalColors = new Color[_hoveredRenderers.Length];

            for (int i = 0; i < _hoveredRenderers.Length; i++)
            {
                var r = _hoveredRenderers[i];
                if (r == null) continue;

                // Guardar color original y aplicar amarillo brillante
                if (r.material.HasProperty("_BaseColor"))
                {
                    _hoveredOriginalColors[i] = r.material.GetColor("_BaseColor");
                    r.material.SetColor("_BaseColor", new Color(1f, 0.95f, 0.1f, 1f));
                }
                else if (r.material.HasProperty("_Color"))
                {
                    _hoveredOriginalColors[i] = r.material.GetColor("_Color");
                    r.material.SetColor("_Color", new Color(1f, 0.95f, 0.1f, 1f));
                }
            }
        }

        /// <summary>
        /// Restaura el color original del objetivo que antes estaba en la mira.
        /// </summary>
        private void ClearHoverHighlight(TargetableEntity target)
        {
            if (target == null || _hoveredRenderers == null) return;

            for (int i = 0; i < _hoveredRenderers.Length; i++)
            {
                var r = _hoveredRenderers[i];
                if (r == null) continue;

                if (r.material.HasProperty("_BaseColor"))
                    r.material.SetColor("_BaseColor", _hoveredOriginalColors[i]);
                else if (r.material.HasProperty("_Color"))
                    r.material.SetColor("_Color", _hoveredOriginalColors[i]);
            }

            _hoveredRenderers      = null;
            _hoveredOriginalColors = null;
        }
        #endregion

        #region Gestión de Objetivos (LIFO)
        public bool SelectTarget(TargetableEntity candidate)
        {
            if (candidate == null) return false;

            if (_target1 == null)
            {
                _target1 = candidate;
                _target1.IsMarkedSlotA = true;
                OnMarksUpdated?.Invoke(_target1, _target2);
                return true;
            }

            if (_target2 == null)
            {
                if (_target1 == candidate)
                {
                    Debug.Log("[BoogieWoogie] No puedes marcar el mismo objetivo dos veces.");
                    return false;
                }
                _target2 = candidate;
                _target2.IsMarkedSlotB = true;
                OnMarksUpdated?.Invoke(_target1, _target2);
                return true;
            }

            Debug.Log("[BoogieWoogie] Ambos objetivos están llenos. Pulsa Q para desmarcar el último.");
            return false;
        }

        public bool SelectSelf()
        {
            if (selfTargetable == null) selfTargetable = GetComponent<TargetableEntity>();
            return SelectTarget(selfTargetable);
        }

        public void UndoLastTarget()
        {
            if (_target2 != null)
            {
                _target2.IsMarkedSlotB = false;
                _target2 = null;
                OnMarksUpdated?.Invoke(_target1, _target2);
                return;
            }

            if (_target1 != null)
            {
                _target1.IsMarkedSlotA = false;
                _target1 = null;
                OnMarksCleared?.Invoke();
                OnMarksUpdated?.Invoke(null, null);
            }
        }

        public void ClearAllTargets()
        {
            if (_target1 != null) _target1.IsMarkedSlotA = false;
            if (_target2 != null) _target2.IsMarkedSlotB = false;
            _target1 = _target2 = null;
            OnMarksCleared?.Invoke();
            OnMarksUpdated?.Invoke(null, null);
        }
        #endregion

        #region Intercambio Vectorial
        public bool TriggerBoogieWoogie()
        {
            if (_target1 == null || _target2 == null || _target1 == _target2) return false;

            if (!_target1.gameObject.activeInHierarchy || !_target2.gameObject.activeInHierarchy)
            {
                UndoLastTarget();
                return false;
            }

            float maxR = CalculateCurrentRange();
            if (Vector3.Distance(transform.position, _target1.transform.position) > maxR ||
                Vector3.Distance(transform.position, _target2.transform.position) > maxR)
            {
                Debug.LogWarning("[BoogieWoogie] Uno de los objetivos está fuera del rango máximo.");
                return false;
            }

            if (!staminaSystem.TryConsumeStamina(swapStaminaCost))
            {
                Debug.LogWarning("[BoogieWoogie] Estamina insuficiente.");
                return false;
            }

            staminaSystem.AddRegenBlocker();

            Vector3 p1 = _target1.transform.position;
            Vector3 p2 = _target2.transform.position;

            TeleportEntity(_target1.gameObject, p2);
            TeleportEntity(_target2.gameObject, p1);
            Physics.SyncTransforms();

            OnSwapExecuted?.Invoke(p1, p2);
            staminaSystem.RemoveRegenBlocker();
            return true;
        }

        private void TeleportEntity(GameObject entity, Vector3 pos)
        {
            if (entity.TryGetComponent<CharacterController>(out var cc))
            {
                cc.enabled = false;
                entity.transform.position = pos;
                cc.enabled = true;
            }
            else if (entity.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.position = entity.transform.position = pos;
            }
            else
            {
                entity.transform.position = pos;
            }
        }
        #endregion

        #region Lectura de Teclas
        private void HandleDirectKeyInputs()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // E: Seleccionar objetivo en mira
            if (kb[selectTargetKey].wasPressedThisFrame && _currentlyHoveredTarget != null)
                SelectTarget(_currentlyHoveredTarget);

            // R: Elegirse a uno mismo
            if (kb[selectSelfKey].wasPressedThisFrame)
                SelectSelf();

            // Q: Desmarcar último
            if (kb[undoLastTargetKey].wasPressedThisFrame)
                UndoLastTarget();

            // F: Intercambiar
            if (kb[activateSwapKey].wasPressedThisFrame)
                TriggerBoogieWoogie();
        }
        #endregion

        #region Gizmos (Scene View)
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, CalculateCurrentRange());

            if (_target1 != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(_target1.transform.position + Vector3.up * 1f, Vector3.one * 0.8f);
            }

            if (_target2 != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireCube(_target2.transform.position + Vector3.up * 1f, Vector3.one * 0.8f);
            }
        }
        #endregion
    }
}
