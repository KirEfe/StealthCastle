using UnityEngine;

namespace StealthCastle.Isometric3D.Hub
{
    /// <summary>
    /// Станция логова: подошёл ближе radius — подсказка, E — открыть окно станции (HubController3D)
    /// </summary>
    public class HubStation3D : MonoBehaviour
    {
        public enum Kind
        {
            StartRun,  // Стол с картой замка — начать забег
            Artifacts, // Верстак — взять/купить артефакт на забег
            Upgrades,  // Скупщик — постоянные улучшения за добычу
            Records,   // Полка трофеев — статистика забегов
        }

        [SerializeField] private Kind kind = Kind.StartRun;
        [SerializeField] private string title = "Map Table";
        [SerializeField] private string prompt = "Plan the heist";
        [SerializeField] private float radius = 2.2f;

        public Kind StationKind => kind;
        public string Title => title;
        public string Prompt => prompt;
        public float Radius => radius;

        public void Configure(Kind stationKind, string stationTitle, string stationPrompt)
        {
            kind = stationKind;
            title = stationTitle;
            prompt = stationPrompt;
        }

        /// <summary>Квадрат расстояния до точки по полу</summary>
        public float FlatSqrDistance(Vector3 position)
        {
            var delta = position - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
