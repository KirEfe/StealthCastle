using UnityEngine;

namespace StealthCastle.Isometric3D.Utils
{
    /// <summary>
    /// Подпись в мире всегда смотрит в камеру (камеру можно вращать)
    /// </summary>
    public class FaceCamera3D : MonoBehaviour
    {
        private Camera cachedCamera;

        private void LateUpdate()
        {
            if (cachedCamera == null) cachedCamera = Camera.main;
            if (cachedCamera != null) transform.rotation = cachedCamera.transform.rotation;
        }
    }
}
