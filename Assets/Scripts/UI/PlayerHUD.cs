using UnityEngine;
using UnityEngine.UI;
using Lucha3D.Core;
using Lucha3D.Abilities;

namespace Lucha3D.UI
{
    /// <summary>
    /// HUD reactivo que muestra la barra de estamina, el multiplicador visual del Índice de Fuerza (x1 a x4),
    /// el temporizador de 5 minutos de la arena, y el estado de las habilidades especiales.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerHUD : MonoBehaviour
    {
        [Header("--- Referencias de Sistemas ---")]
        [SerializeField] private HealthSystem playerHealth;
        [SerializeField] private StaminaSystem staminaSystem;
        [SerializeField] private AbilityLoadoutManager loadoutManager;
        [SerializeField] private ArenaResetManager resetManager;
        [SerializeField] private BoogieWoogieAbility boogieWoogie;
        [SerializeField] private ProjectionTechnique projectionTechnique;

        [Header("--- Elementos de UI de Estamina y Fuerza ---")]
        [Tooltip("Imagen radial o barra horizontal con FillMethod para la estamina.")]
        [SerializeField] private Image staminaBarFill;

        [Tooltip("Texto que muestra la estamina numérica (ej: 285 / 300).")]
        [SerializeField] private Text staminaNumericText;

        [Tooltip("Texto que muestra el multiplicador de fuerza (ej: x1.0, x3.85, x4.0 MAX).")]
        [SerializeField] private Text strengthIndexText;

        [Tooltip("Gradiente de color para la barra según la fuerza (de x1 azul/verde a x4 rojo/dorado).")]
        [SerializeField] private Gradient strengthGradient;

        [Header("--- Elementos de UI de Arena y Habilidades ---")]
        [Tooltip("Texto del temporizador de reinicio de 5 minutos.")]
        [SerializeField] private Text arenaResetTimerText;

        [Tooltip("Texto o indicador de objetivos de Boogie-Woogie (Slot A y Slot B).")]
        [SerializeField] private Text boogieWoogieStatusText;

        [Tooltip("Indicador visual de la ventana de inputs de la Técnica de Proyección.")]
        [SerializeField] private GameObject projectionInputWindowIndicator;

        private void Start()
        {
            if (staminaSystem == null) staminaSystem = FindFirstObjectByType<StaminaSystem>();
            if (playerHealth == null && staminaSystem != null) playerHealth = staminaSystem.GetComponent<HealthSystem>();
            if (playerHealth == null) playerHealth = FindFirstObjectByType<HealthSystem>();
            if (loadoutManager == null) loadoutManager = FindFirstObjectByType<AbilityLoadoutManager>();
            if (resetManager == null) resetManager = FindFirstObjectByType<ArenaResetManager>();
            if (boogieWoogie == null) boogieWoogie = FindFirstObjectByType<BoogieWoogieAbility>();
            if (projectionTechnique == null) projectionTechnique = FindFirstObjectByType<ProjectionTechnique>();

            SubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void SubscribeEvents()
        {
            if (staminaSystem != null)
            {
                staminaSystem.OnStaminaChanged += UpdateStaminaDisplay;
                staminaSystem.OnStrengthIndexChanged += UpdateStrengthDisplay;

                // Actualización inicial
                UpdateStaminaDisplay(staminaSystem.CurrentStamina, staminaSystem.MaxStamina);
                UpdateStrengthDisplay(staminaSystem.CurrentStrengthIndex);
            }

            if (resetManager != null)
            {
                resetManager.OnTimerTick += UpdateArenaTimer;
            }

            if (boogieWoogie != null)
            {
                boogieWoogie.OnMarksUpdated += UpdateBoogieMarks;
            }

            if (projectionTechnique != null)
            {
                projectionTechnique.OnInputWindowOpened += HandleProjectionWindowOpened;
                projectionTechnique.OnBurstExecutionStarted += HandleProjectionBurstStarted;
                projectionTechnique.OnTechniqueCompleted += HandleProjectionCompleted;
            }
        }

        private void UnsubscribeEvents()
        {
            if (staminaSystem != null)
            {
                staminaSystem.OnStaminaChanged -= UpdateStaminaDisplay;
                staminaSystem.OnStrengthIndexChanged -= UpdateStrengthDisplay;
            }

            if (resetManager != null)
            {
                resetManager.OnTimerTick -= UpdateArenaTimer;
            }

            if (boogieWoogie != null)
            {
                boogieWoogie.OnMarksUpdated -= UpdateBoogieMarks;
            }

            if (projectionTechnique != null)
            {
                projectionTechnique.OnInputWindowOpened -= HandleProjectionWindowOpened;
                projectionTechnique.OnBurstExecutionStarted -= HandleProjectionBurstStarted;
                projectionTechnique.OnTechniqueCompleted -= HandleProjectionCompleted;
            }
        }

        #region Actualización de HUD
        private void UpdateStaminaDisplay(float current, float max)
        {
            if (staminaBarFill != null)
            {
                staminaBarFill.fillAmount = Mathf.Clamp01(current / max);
            }

            if (staminaNumericText != null)
            {
                staminaNumericText.text = $"{Mathf.RoundToInt(current)} / {Mathf.RoundToInt(max)}";
            }
        }

        private void UpdateStrengthDisplay(float strengthIndex)
        {
            if (strengthIndexText != null)
            {
                strengthIndexText.text = strengthIndex >= 3.99f ? "x4.0 MAX" : $"x{strengthIndex:F2}";
            }

            if (staminaBarFill != null && strengthGradient != null)
            {
                float t = Mathf.Clamp01((strengthIndex - 1.0f) / 3.0f);
                staminaBarFill.color = strengthGradient.Evaluate(t);
            }
        }

        private void UpdateArenaTimer(float remainingSeconds)
        {
            if (arenaResetTimerText != null)
            {
                int minutes = Mathf.FloorToInt(remainingSeconds / 60f);
                int seconds = Mathf.FloorToInt(remainingSeconds % 60f);
                arenaResetTimerText.text = $"{minutes:00}:{seconds:00}";
            }
        }

        private void UpdateBoogieMarks(TargetableEntity slotA, TargetableEntity slotB)
        {
            if (boogieWoogieStatusText != null)
            {
                string nameA = slotA != null ? slotA.EntityDisplayName : "[Vacío]";
                string nameB = slotB != null ? slotB.EntityDisplayName : "[Vacío]";
                boogieWoogieStatusText.text = $"Boogie-Woogie: [A: {nameA}] <-> [B: {nameB}]";
            }
        }

        private void HandleProjectionWindowOpened(float duration)
        {
            if (projectionInputWindowIndicator != null)
            {
                projectionInputWindowIndicator.SetActive(true);
            }
        }

        private void HandleProjectionBurstStarted(int commandCount)
        {
            if (projectionInputWindowIndicator != null)
            {
                projectionInputWindowIndicator.SetActive(false);
            }
        }

        private void HandleProjectionCompleted()
        {
            if (projectionInputWindowIndicator != null)
            {
                projectionInputWindowIndicator.SetActive(false);
            }
        }
        #endregion

        #region Fallback Visual Directo (OnGUI de Alto Rendimiento)
        private void OnGUI()
        {
            // Si no se asignaron elementos uGUI en el Inspector, mostramos un HUD flotante estilizado automático
            if (staminaBarFill == null || staminaNumericText == null)
            {
                DrawDirectHUD();
            }
        }

        private void DrawDirectHUD()
        {
            float health = playerHealth != null ? playerHealth.CurrentHealth : 200f;
            float maxHealth = playerHealth != null ? playerHealth.MaxHealth : 200f;
            float stamina = staminaSystem != null ? staminaSystem.CurrentStamina : 300f;
            float maxStam = staminaSystem != null ? staminaSystem.MaxStamina : 300f;
            float strength = staminaSystem != null ? staminaSystem.CurrentStrengthIndex : 1.0f;
            float timer = resetManager != null ? resetManager.TimeRemaining : 300f;

            // 1. Contenedor de Recursos (Vida y Estamina)
            GUI.Box(new Rect(20, 20, 330, 160), "ESTADO DEL LUCHADOR");

            // Barra de Vida (200 HP)
            float healthPct = Mathf.Clamp01(health / maxHealth);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(30, 44, 310, 20), Texture2D.whiteTexture);
            GUI.color = Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(0.2f, 0.85f, 0.3f), healthPct);
            GUI.DrawTexture(new Rect(31, 45, 308 * healthPct, 18), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string regenNotice = (playerHealth != null && playerHealth.IsRegenerating) ? " (+20/s)" : "";
            GUI.Label(new Rect(35, 45, 300, 20), $"Salud: {health:F0} / {maxHealth:F0} HP{regenNotice}");

            // Barra de Estamina (300 pts)
            float fillPct = Mathf.Clamp01(stamina / maxStam);
            Color barColor = Color.Lerp(new Color(0.2f, 0.7f, 1f), new Color(1f, 0.4f, 0.1f), (strength - 1f) / 3f);
            
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(30, 72, 310, 20), Texture2D.whiteTexture);
            GUI.color = barColor;
            GUI.DrawTexture(new Rect(31, 73, 308 * fillPct, 18), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(35, 73, 300, 20), $"Estamina: {stamina:F0} / {maxStam:F0} pts");

            // Multiplicador de Fuerza
            string strLabel = strength >= 3.99f ? "x4.00 MAX" : $"x{strength:F2}";
            GUI.Label(new Rect(35, 100, 300, 20), $"Índice de Fuerza: {strLabel}");

            // Temporizador de Arena (5 minutos)
            int min = Mathf.FloorToInt(timer / 60f);
            int sec = Mathf.FloorToInt(timer % 60f);
            GUI.Label(new Rect(35, 125, 300, 20), $"Reinicio de Arena en: {min:00}:{sec:00}");

            // 2. Estado de Habilidades Especiales y Poder Equipado
            string abilityName = loadoutManager != null ? loadoutManager.CurrentAbilityDisplayName : "Selección";
            GUI.Box(new Rect(20, 190, 330, 115), $"HABILIDAD: {abilityName.ToUpper()}");

            if (boogieWoogie != null && boogieWoogie.enabled)
            {
                string t1 = boogieWoogie.Target1 != null ? boogieWoogie.Target1.EntityDisplayName : "[Vacío]";
                string t2 = boogieWoogie.Target2 != null ? boogieWoogie.Target2.EntityDisplayName : "[Vacío]";
                GUI.Label(new Rect(35, 215, 300, 20), $"1º Objetivo: {t1}");
                GUI.Label(new Rect(35, 235, 300, 20), $"2º Objetivo: {t2}");
                GUI.Label(new Rect(35, 255, 300, 20), $"Rango: {boogieWoogie.CurrentRange:F1} m (E: Marcar | R: Uno mismo | Q: Deshacer)");
                if (boogieWoogie.CanSwap)
                {
                    GUI.color = Color.cyan;
                    GUI.Label(new Rect(35, 275, 300, 20), "¡LISTO! Pulsa 'F' para Intercambiar.");
                    GUI.color = Color.white;
                }
            }
            else if (projectionTechnique != null && projectionTechnique.enabled)
            {
                if (projectionTechnique.IsTechniqueActive)
                {
                    GUI.color = Color.yellow;
                    string projStatus = projectionTechnique.IsInputWindowOpen ? "¡VENTANA DE INPUTS ABIERTA!" : "PREPARACIÓN / RÁFAGA 24 FPS";
                    GUI.Label(new Rect(35, 220, 300, 20), $"Estado: {projStatus}");
                    GUI.Label(new Rect(35, 245, 300, 20), $"Inputs en buffer: {projectionTechnique.BufferedInputCount}");
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.Label(new Rect(35, 225, 300, 20), "Pulsa 'Q' para activar (50 pts estamina)");
                    GUI.Label(new Rect(35, 250, 300, 20), "Ventana: ↑↓←→ Mover | E Atacar | Q Sprint | F Agacharse");
                }
            }
            else
            {
                GUI.Label(new Rect(35, 235, 300, 20), "Presiona TAB para elegir un poder.");
            }

            // 3. Monitor de Maniquíes en la Arena (200 HP)
            var dummies = FindObjectsByType<Combat.TrainingDummy>(FindObjectsSortMode.None);
            if (dummies != null && dummies.Length > 0)
            {
                GUI.Box(new Rect(Screen.width - 300, 245, 280, 45 + dummies.Length * 26), "MANIQUÍES EN ARENA (200 HP)");
                int dy = 270;
                foreach (var d in dummies)
                {
                    if (d == null) continue;
                    float dHp = d.Health != null ? d.Health.CurrentHealth : 200f;
                    string dStatus = d.gameObject.activeInHierarchy ? $"{dHp:F0} / 200 HP" : "[DESAPARECIDO / 0 HP]";
                    GUI.Label(new Rect(Screen.width - 290, dy, 260, 20), $"• {d.gameObject.name}: {dStatus}");
                    dy += 24;
                }
            }

            // 4. Guía de Controles
            GUI.Box(new Rect(Screen.width - 300, 20, 280, 235), "CONTROLES DE PRUEBA");
            GUI.Label(new Rect(Screen.width - 290, 42, 265, 205),
                "• TAB: Cambiar Poder Especial (Menú)\n" +
                "• WASD: Correr (10 km/h) | Shift: Sprint\n" +
                "• Alt: Andar (3 km/h) | Espacio: Salto\n" +
                "• Clic Izq / J: Ataque | Mantener: Carga\n" +
                "• C/Ctrl + Ataque Abajo: Arrancar escombro\n" +
                "--- BOOGIE-WOOGIE ---\n" +
                "• E: Marcar Objetivo (1º y 2º en orden)\n" +
                "• R: Elegirse a uno mismo (1º o 2º)\n" +
                "• Q: Deshacer último objetivo\n" +
                "• F: Intercambiar Posiciones (Swap)\n" +
                "--- TÉCNICA DE PROYECCIÓN ---\n" +
                "• Q: Activar (50 pts) → prep 0.5s\n" +
                "• Ventana: ↑↓←→ Mover | E Atacar\n" +
                "            Q Sprint | F Agacharse");
        }
        #endregion
    }
}
