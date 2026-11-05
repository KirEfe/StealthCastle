using UnityEngine;
using UnityEngine.Rendering;

namespace StealthCastle.Isometric3D.Enemies
{
    /// <summary>
    /// Видимый конус зрения стража на полу. Строится лучами с теми же параметрами,
    /// что и EnemyAI3D.CanSeePlayer (направление, дальность, угол, слой препятствий),
    /// поэтому обрезается стенами и показывает ровно ту зону, где игрока замечают.
    /// Цвет — по состоянию: патруль, тревога (Investigate/Search), погоня.
    /// </summary>
    [RequireComponent(typeof(EnemyAI3D))]
    public class VisionCone3D : MonoBehaviour
    {
        [SerializeField] private Material coneMaterial; // Прозрачный материал; пусто — создаётся в рантайме
        [SerializeField, Range(8, 64)] private int segments = 32; // Лучей на дугу
        [SerializeField] private float groundOffset = 0.03f; // Над полом, чтобы не мерцал

        [Header("Colors")]
        [SerializeField] private Color patrolColor = new Color(1f, 0.9f, 0.3f, 0.22f);
        [SerializeField] private Color alertColor = new Color(1f, 0.55f, 0.1f, 0.3f);
        [SerializeField] private Color chaseColor = new Color(1f, 0.15f, 0.1f, 0.35f);

        private EnemyAI3D ai;
        private Collider bodyCollider;
        private Transform coneTransform;
        private Material material;
        private Mesh mesh;
        private Vector3[] vertices;

        private void Awake()
        {
            ai = GetComponent<EnemyAI3D>();
            bodyCollider = GetComponent<Collider>();

            var coneGo = new GameObject("VisionCone");
            coneTransform = coneGo.transform;
            coneTransform.SetParent(transform, false);

            mesh = new Mesh { name = "VisionCone" };
            mesh.MarkDynamic();
            vertices = new Vector3[segments + 2];

            // Веер треугольников из центра; угол растёт по часовой при взгляде сверху
            var triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;

            coneGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var coneRenderer = coneGo.AddComponent<MeshRenderer>();
            coneRenderer.shadowCastingMode = ShadowCastingMode.Off;
            coneRenderer.receiveShadows = false;

            material = coneMaterial != null ? new Material(coneMaterial) : CreateDefaultMaterial();
            coneRenderer.sharedMaterial = material;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (mesh != null) Destroy(mesh);
        }

        private void LateUpdate()
        {
            // Тело стража не вращается — конус строим в мировых координатах
            coneTransform.rotation = Quaternion.identity;

            Vector3 origin = transform.position;
            Vector3 forward = ai.ViewForward;
            float range = ai.VisionRange;
            float fov = ai.FovDegrees;
            LayerMask mask = ai.ObstacleMask;
            float groundY = (bodyCollider != null ? bodyCollider.bounds.min.y : origin.y) + groundOffset;

            vertices[0] = coneTransform.InverseTransformPoint(new Vector3(origin.x, groundY, origin.z));
            for (int i = 0; i <= segments; i++)
            {
                float angle = -fov * 0.5f + fov * i / segments;
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;

                // Лучи на высоте глаз, как в CanSeePlayer: стена обрезает конус
                float distance = Physics.Raycast(origin, direction, out RaycastHit hit, range, mask, QueryTriggerInteraction.Ignore)
                    ? hit.distance
                    : range;

                Vector3 point = origin + direction * distance;
                point.y = groundY;
                vertices[i + 1] = coneTransform.InverseTransformPoint(point);
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();

            material.color = ai.CurrentState switch
            {
                EnemyAI3D.EnemyState.Chase => chaseColor,
                EnemyAI3D.EnemyState.Investigate => alertColor,
                EnemyAI3D.EnemyState.Search => alertColor,
                _ => patrolColor,
            };
        }

        /// <summary>
        /// Прозрачный двусторонний URP Unlit. Используется в рантайме и редакторным билдером сцены.
        /// </summary>
        public static Material CreateDefaultMaterial()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "VisionCone" };
            material.SetFloat("_Surface", 1f); // Transparent
            material.SetFloat("_Blend", 0f); // Alpha
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }
    }
}
