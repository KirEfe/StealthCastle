using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace StealthCastle.Isometric.UI
{
    /// <summary>
    /// Выбор артефакта на стартовом экране.
    /// Эффект виден уже на этапе сборки — веса применяются до генерации.
    /// </summary>
    public class ArtifactSelector : MonoBehaviour
    {
        [SerializeField] private List<Core.ArtifactData> availableArtifacts = new();
        [SerializeField] private World.GenerationRules rules; // правила, куда пишутся модификаторы
        [SerializeField] private RectTransform buttonsContainer;  // контейнер кнопок
        [SerializeField] private Button buttonPrefab; // префаб кнопки (с TextMeshProUGUI)

        private Core.ArtifactData selected;
        private Action<World.GenerationRules> onConfirmed;

        /// <summary>Показать выбор артефакта; callback после подтверждения.</summary>
        public void Show(Action<World.GenerationRules> confirmed)
        {
            onConfirmed = confirmed;
            gameObject.SetActive(true);
            RebuildButtons();
        }

        // Построение кнопок по списку артефактов
        private void RebuildButtons()
        {
            foreach (Transform child in buttonsContainer) Destroy(child.gameObject);

            foreach (var artifact in availableArtifacts)
            {
                if (artifact == null) continue;
                var btn = Instantiate(buttonPrefab, buttonsContainer);
                var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null)
                    txt.text = $"{artifact.DisplayName}\n{artifact.Description}";
                var captured = artifact;
                btn.onClick.AddListener(() => Select(captured));
            }

            // Кнопка старта забега
            var start = Instantiate(buttonPrefab, buttonsContainer);
            var startTxt = start.GetComponentInChildren<TextMeshProUGUI>();
            if (startTxt != null)
                startTxt.text = "Начать забег";
            start.onClick.AddListener(() => onConfirmed?.Invoke(rules));
        }

        // Выбор артефакта (запоминаем, применение — в BeginRun через ApplySelected)
        private void Select(Core.ArtifactData artifact)
        {
            selected = artifact;
        }

        /// <summary>Применить выбранный артефакт перед генерацией (единожды).</summary>
        public void ApplySelected()
        {
            if (selected != null && rules != null)
            {
                rules.ClearTagMultipliers(); // сброс предыдущего выбора
                selected.Apply(rules);
            }
            gameObject.SetActive(false);
        }

        /// <summary>Вернуть выбранный артефакт (для применения бонусов игрока в RunManager).</summary>
        public Core.ArtifactData GetSelected() => selected;
    }
}
