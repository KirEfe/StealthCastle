using System.Collections.Generic;
using UnityEngine;

namespace StealthCastle.Isometric3D.Utils
{
    /// <summary>
    /// Утилитарные функции для работы с физикой в 3D изометрической игре.
    /// </summary>
    public static class PhysicsUtils3D
    {
        /// <summary>
        /// Проверка видимости между двумя точками (без учёта слоёв)
        /// </summary>
        public static bool IsVisible(Vector3 from, Vector3 to, LayerMask obstacleMask)
        {
            RaycastHit hit;
            Vector3 direction = (to - from).normalized;
            float distance = Vector3.Distance(from, to);
            
            return !Physics.Raycast(from, direction, out hit, distance, obstacleMask);
        }

        /// <summary>
        /// Проверка видимости с учётом угла обзора
        /// </summary>
        public static bool IsVisibleWithFOV(
            Vector3 observerPosition, 
            Vector3 observerForward, 
            Vector3 targetPosition, 
            float fovDegrees, 
            float maxDistance, 
            LayerMask obstacleMask)
        {
            // Проверка расстояния
            float distance = Vector3.Distance(observerPosition, targetPosition);
            if (distance > maxDistance) return false;

            // Проверка угла обзора
            Vector3 toTarget = (targetPosition - observerPosition).normalized;
            float angle = Vector3.Angle(observerForward, toTarget);
            if (angle > fovDegrees * 0.5f) return false;

            // Проверка препятствий
            return IsVisible(observerPosition, targetPosition, obstacleMask);
        }

        /// <summary>
        /// Проверка нахождения точки в зоне обзора
        /// </summary>
        public static bool IsInFOVCone(
            Vector3 observerPosition, 
            Vector3 observerForward, 
            Vector3 targetPosition, 
            float fovDegrees)
        {
            Vector3 toTarget = (targetPosition - observerPosition).normalized;
            float angle = Vector3.Angle(observerForward, toTarget);
            return angle <= fovDegrees * 0.5f;
        }

        /// <summary>
        /// Получение точки на земле под объектом
        /// </summary>
        public static Vector3 GetGroundPoint(Vector3 origin, LayerMask groundMask, float maxDistance = 100f)
        {
            RaycastHit hit;
            if (Physics.Raycast(origin, Vector3.down, out hit, maxDistance, groundMask))
            {
                return hit.point;
            }
            return origin;
        }

        /// <summary>
        /// Проверка нахождения объекта на земле
        /// </summary>
        public static bool IsGrounded(Transform transform, LayerMask groundMask, float checkDistance = 0.2f)
        {
            RaycastHit hit;
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            return Physics.Raycast(origin, Vector3.down, out hit, checkDistance + 0.1f, groundMask);
        }

        /// <summary>
        /// Получение нормали поверхности в точке
        /// </summary>
        public static Vector3 GetSurfaceNormal(Vector3 position, LayerMask groundMask)
        {
            RaycastHit hit;
            if (Physics.Raycast(position + Vector3.up * 0.1f, Vector3.down, out hit, 1f, groundMask))
            {
                return hit.normal;
            }
            return Vector3.up;
        }

        /// <summary>
        /// Проверка столкновения с капсулой
        /// </summary>
        public static bool CheckCapsuleCollision(
            Vector3 position, 
            float radius, 
            float height, 
            LayerMask collisionMask)
        {
            return Physics.CheckCapsule(
                position + Vector3.up * (height / 2f - radius),
                position + Vector3.up * (radius - height / 2f),
                radius,
                collisionMask
            );
        }

        /// <summary>
        /// Поиск ближайшей точки на коллайдере
        /// </summary>
        public static Vector3 FindClosestPointOnCollider(Collider collider, Vector3 point)
        {
            return collider.ClosestPoint(point);
        }

        /// <summary>
        /// Проверка нахождения точки внутри коллайдера
        /// </summary>
        public static bool IsPointInsideCollider(Collider collider, Vector3 point)
        {
            return collider.bounds.Contains(point);
        }

        /// <summary>
        /// Получение размеров коллайдера
        /// </summary>
        public static Vector3 GetColliderSize(Collider collider)
        {
            if (collider is BoxCollider box)
            {
                return box.size;
            }
            else if (collider is SphereCollider sphere)
            {
                return Vector3.one * sphere.radius * 2f;
            }
            else if (collider is CapsuleCollider capsule)
            {
                return new Vector3(
                    capsule.radius * 2f,
                    capsule.height,
                    capsule.radius * 2f
                );
            }
            else if (collider is MeshCollider mesh)
            {
                return mesh.bounds.size;
            }
            
            return Vector3.one;
        }

        /// <summary>
        /// Создание триггерного коллайдера для зоны обнаружения
        /// </summary>
        public static GameObject CreateDetectionZone(
            Transform parent, 
            Vector3 center, 
            Vector3 size, 
            LayerMask detectLayer)
        {
            var zone = new GameObject("DetectionZone");
            zone.transform.SetParent(parent, false);
            zone.transform.localPosition = center;
            
            var collider = zone.AddComponent<BoxCollider>();
            collider.size = size;
            collider.isTrigger = true;
            
            var detection = zone.AddComponent<DetectionZone>();
            detection.SetLayer(detectLayer);
            
            return zone;
        }

        /// <summary>
        /// Получение направления от объекта к цели
        /// </summary>
        public static Vector3 GetDirectionToTarget(Transform observer, Vector3 target)
        {
            return (target - observer.position).normalized;
        }

        /// <summary>
        /// Проверка попадания луча в объект
        /// </summary>
        public static bool RaycastToObject(GameObject obj, Vector3 origin, LayerMask mask)
        {
            if (obj == null) return false;
            
            var collider = obj.GetComponent<Collider>();
            if (collider == null) return false;
            
            RaycastHit hit;
            Vector3 direction = (collider.bounds.center - origin).normalized;
            float distance = Vector3.Distance(origin, collider.bounds.center) + collider.bounds.extents.magnitude;
            
            return Physics.Raycast(origin, direction, out hit, distance, mask) && hit.collider.gameObject == obj;
        }
    }

    /// <summary>
    /// Компонент зоны обнаружения
    /// </summary>
    public class DetectionZone : MonoBehaviour
    {
        private LayerMask detectLayer;
        private List<GameObject> detectedObjects = new List<GameObject>();

        /// <summary>Список обнаруженных объектов</summary>
        public IReadOnlyList<GameObject> DetectedObjects => detectedObjects;

        /// <summary>Установка слоя для обнаружения</summary>
        public void SetLayer(LayerMask layer)
        {
            detectLayer = layer;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (detectedObjects.Contains(other.gameObject)) return;
            
            // Проверяем слой
            if ((detectLayer.value & (1 << other.gameObject.layer)) != 0)
            {
                detectedObjects.Add(other.gameObject);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            detectedObjects.Remove(other.gameObject);
        }

        /// <summary>
        /// Проверка наличия объекта в зоне
        /// </summary>
        public bool Contains(GameObject obj)
        {
            return detectedObjects.Contains(obj);
        }

        /// <summary>
        /// Очистка списка
        /// </summary>
        public void Clear()
        {
            detectedObjects.Clear();
        }
    }
}