using UnityEngine;
using UnityEngine.InputSystem;

namespace _2DCameraAim
{
    [RequireComponent(typeof(Camera))]
    public class AimCameraComponent : MonoBehaviour
    {
        public enum eCurve
        {
            Linear,
            Square,
            InvertedSquare,
            Sine
        }

        [SerializeField, Tooltip("Object the camera will follow")]
        private GameObject player;
        private Camera _cam;
        private Vector2 _displace = Vector2.zero;

        [Tooltip(
            "Function to use when interpolating the position:\n* Linear: Will increase the distance the same " +
            "amount at any mouse position.\n* Square: will increase the distance, very little when the mouse is " +
            "near the inner zone, but way more when the mouse is near the outer zone.\n* InvertedSquare: will " +
            "increase the distance, very a lot when the mouse is near the inner zone, but way less when the mouse " +
            "is near the outer zone.\n* Sine: the distance will increase little near the zone edges but will change " +
            "faster while in the center of the area."
        )]
        public eCurve curve = eCurve.InvertedSquare;
        
        [Range(0.0f, 1.0f),
         Tooltip(
             "Mouse position at which the camera will start moving away from the character. measured as a " +
             "percentage from the center to an edge of the screen"
         )]
        public float innerZone = 0.2f;
        
        [Range(0.0f, 1.0f),
         Tooltip(
             "Mouse position at which the camera will stop moving away from the character. measured as a percentage " +
             "from the center to an edge of the screen"
         )]
        public float outerZone = 0.8f;

        [Min(0.0f),
         Tooltip(
             "The maximum distance the camera acn be from the player, measured as a percentage of the distance " +
             "from the player to the cursor."
         )]
        public float maxDistance = 0.7f;

       [Tooltip("The camera offset from the player on the z axis.")]
        public float offset = 10;

        [Min(0.0f), Tooltip("The time it will take the camera to reach the desired position.")]
        public float smoothSpeed = 0.2f;

        private Vector3 _velocity;
        
        [Header("Debug")]
        [SerializeField]
        private float distance = 0;
        [SerializeField]
        private Vector2 vieportDisplace;
        [SerializeField]
        private Vector2 line;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            // Получаем позицию мыши через новый InputSystem
            Vector2 mouseScreenPos = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : new Vector2(Screen.width / 2f, Screen.height / 2f);
 
            // Позиция мыши от -0.5 до 0.5 относительно центра экрана
            line = (Vector2)_cam.ScreenToViewportPoint(mouseScreenPos) - new Vector2(0.5f, 0.5f);

            distance = 0;
            if (line.magnitude < innerZone / 2f) distance = 0;
            else if (line.magnitude > outerZone / 2f) distance = maxDistance;
            else
            {
                float a = 0;
                switch (curve)
                {
                    case eCurve.Linear:
                        Vector2 p1 = new Vector2(innerZone, 0);
                        Vector2 p2 = new Vector2(outerZone, maxDistance);
                        float m = (p1.y - p2.y) / (p1.x - p2.x);
                        distance = m * (line.magnitude * 2 - p1.x) - p1.y;
                        break;

                    case eCurve.Square:
                        a = maxDistance / Mathf.Pow(outerZone - innerZone, 2);
                        distance = a * Mathf.Pow(line.magnitude * 2 - innerZone, 2);
                        break;
                    case eCurve.InvertedSquare:
                        a = -maxDistance / Mathf.Pow(innerZone - outerZone, 2);
                        distance = a * Mathf.Pow(line.magnitude * 2 - outerZone, 2) + maxDistance;
                        break;

                    case eCurve.Sine:
                        float amplitude = maxDistance / 2;
                        float frectuence = outerZone - innerZone;
                        float phase = frectuence - innerZone;
                        distance =
                            amplitude * Mathf.Cos((Mathf.PI / frectuence) * (line.magnitude * 2 + phase)) +
                            amplitude;
                        break;
                }
            }

            vieportDisplace = (Vector2)_cam.WorldToViewportPoint(new Vector3(0, 0, 0)) + line * distance;
            _displace = _cam.ViewportToWorldPoint(vieportDisplace);
            Vector3 targetPos = new Vector3(player.transform.position.x + _displace.x,
                player.transform.position.y + _displace.y, -offset);

            Vector3 smoothedPos = Vector3.SmoothDamp(_cam.transform.position, targetPos, ref _velocity, smoothSpeed);
            _cam.transform.position = smoothedPos;
        }
    }
}