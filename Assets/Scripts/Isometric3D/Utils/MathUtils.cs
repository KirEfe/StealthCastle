using UnityEngine;
using System.Collections.Generic;

namespace StealthCastle.Isometric3D.Utils
{
    /// <summary>
    /// Математические утилиты для 3D изометрической игры.
    /// </summary>
    public static class MathUtils
    {
        /// <summary>
        /// Преобразование 2D вектора в изометрическое 3D направление
        /// </summary>
        public static Vector3 ToIsometricDirection(Vector2 input)
        {
            // Коррекция осей для изометрии
            const float isoScale = 0.5774f; // ~1/√3
            return new Vector3(
                input.x,
                0f,
                input.y * isoScale
            ).normalized;
        }

        /// <summary>
        /// Преобразование 2D вектора в изометрическую позицию
        /// </summary>
        public static Vector3 ToIsometricPosition(Vector2 input)
        {
            return new Vector3(
                input.x,
                0f,
                input.y * 0.5774f
            );
        }

        /// <summary>
        /// Преобразование изометрической позиции обратно в 2D
        /// </summary>
        public static Vector2 FromIsometricPosition(Vector3 position)
        {
            return new Vector2(
                position.x,
                position.z / 0.5774f
            );
        }

        /// <summary>
        /// Вычисление расстояния в изометрической проекции
        /// </summary>
        public static float IsometricDistance(Vector3 a, Vector3 b)
        {
            // В изометрии используем обычное 3D расстояние, но можно добавить коррекцию
            return Vector3.Distance(a, b);
        }

        /// <summary>
        /// Вычисление изометрического расстояния между 2D точками
        /// </summary>
        public static float IsometricDistance(Vector2 a, Vector2 b)
        {
            Vector3 a3D = ToIsometricPosition(a);
            Vector3 b3D = ToIsometricPosition(b);
            return Vector3.Distance(a3D, b3D);
        }

        /// <summary>
        /// Нормализация угла в диапазоне 0-360 градусов
        /// </summary>
        public static float NormalizeAngle(float angle)
        {
            angle = angle % 360f;
            if (angle < 0) angle += 360f;
            return angle;
        }

        /// <summary>
        /// Преобразование угла в радианы
        /// </summary>
        public static float DegreesToRadians(float degrees)
        {
            return degrees * Mathf.Deg2Rad;
        }

        /// <summary>
        /// Преобразование радиан в градусы
        /// </summary>
        public static float RadiansToDegrees(float radians)
        {
            return radians * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Вычисление угла между двумя векторами в градусах
        /// </summary>
        public static float AngleBetweenVectors(Vector3 a, Vector3 b)
        {
            return Vector3.Angle(a, b);
        }

        /// <summary>
        /// Вычисление знакового угла между двумя векторами
        /// </summary>
        public static float SignedAngleBetweenVectors(Vector3 a, Vector3 b, Vector3 normal)
        {
            return Vector3.SignedAngle(a, b, normal);
        }

        /// <summary>
        /// Проверка, находится ли угол в заданном диапазоне
        /// </summary>
        public static bool IsAngleInRange(float angle, float minAngle, float maxAngle)
        {
            angle = NormalizeAngle(angle);
            minAngle = NormalizeAngle(minAngle);
            maxAngle = NormalizeAngle(maxAngle);

            if (minAngle <= maxAngle)
            {
                return angle >= minAngle && angle <= maxAngle;
            }
            else
            {
                return angle >= minAngle || angle <= maxAngle;
            }
        }

        /// <summary>
        /// Линейная интерполяция угла
        /// </summary>
        public static float LerpAngle(float a, float b, float t)
        {
            return Mathf.LerpAngle(a, b, t);
        }

        /// <summary>
        /// Плавное затухание
        /// </summary>
        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime)
        {
            return Mathf.SmoothDamp(current, target, ref currentVelocity, smoothTime);
        }

        /// <summary>
        /// Плавное затухание для Vector3
        /// </summary>
        public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime)
        {
            return Vector3.SmoothDamp(current, target, ref currentVelocity, smoothTime);
        }

        /// <summary>
        /// Проверка, находится ли точка в пределах прямоугольника
        /// </summary>
        public static bool IsPointInRect(Vector2 point, Vector2 min, Vector2 max)
        {
            return point.x >= min.x && point.x <= max.x &&
                   point.y >= min.y && point.y <= max.y;
        }

        /// <summary>
        /// Генерация случайной точки в пределах прямоугольника
        /// </summary>
        public static Vector2 RandomPointInRect(Vector2 min, Vector2 max)
        {
            return new Vector2(
                Random.Range(min.x, max.x),
                Random.Range(min.y, max.y)
            );
        }

        /// <summary>
        /// Генерация случайной точки на окружности
        /// </summary>
        public static Vector2 RandomPointOnCircle(float radius)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            return new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius
            );
        }

        /// <summary>
        /// Генерация случайной точки в круге
        /// </summary>
        public static Vector2 RandomPointInCircle(float radius)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(Random.Range(0f, 1f)) * radius;
            return new Vector2(
                Mathf.Cos(angle) * r,
                Mathf.Sin(angle) * r
            );
        }

        /// <summary>
        /// Проверка, находится ли точка в круге
        /// </summary>
        public static bool IsPointInCircle(Vector2 point, Vector2 center, float radius)
        {
            return Vector2.Distance(point, center) <= radius;
        }

        /// <summary>
        /// Вычисление центра масс списка точек
        /// </summary>
        public static Vector3 CalculateCenterOfMass(Vector3[] points)
        {
            if (points == null || points.Length == 0) return Vector3.zero;

            Vector3 center = Vector3.zero;
            foreach (var point in points)
            {
                center += point;
            }
            return center / points.Length;
        }

        /// <summary>
        /// Вычисление среднего значения
        /// </summary>
        public static float Average(float[] values)
        {
            if (values == null || values.Length == 0) return 0f;

            float sum = 0f;
            foreach (var value in values)
            {
                sum += value;
            }
            return sum / values.Length;
        }

        /// <summary>
        /// Вычисление медианы
        /// </summary>
        public static float Median(float[] values)
        {
            if (values == null || values.Length == 0) return 0f;

            System.Array.Sort(values);
            int middle = values.Length / 2;
            
            if (values.Length % 2 == 1)
            {
                return values[middle];
            }
            else
            {
                return (values[middle - 1] + values[middle]) / 2f;
            }
        }

        /// <summary>
        /// Ограничение значения в диапазоне
        /// </summary>
        public static float Clamp(float value, float min, float max)
        {
            return Mathf.Clamp(value, min, max);
        }

        /// <summary>
        /// Ограничение вектора в диапазоне
        /// </summary>
        public static Vector3 Clamp(Vector3 value, Vector3 min, Vector3 max)
        {
            return new Vector3(
                Mathf.Clamp(value.x, min.x, max.x),
                Mathf.Clamp(value.y, min.y, max.y),
                Mathf.Clamp(value.z, min.z, max.z)
            );
        }

        /// <summary>
        /// Проверка равенства векторов с учетом точности
        /// </summary>
        public static bool Approximately(Vector3 a, Vector3 b, float epsilon = 0.0001f)
        {
            return Mathf.Abs(a.x - b.x) < epsilon &&
                   Mathf.Abs(a.y - b.y) < epsilon &&
                   Mathf.Abs(a.z - b.z) < epsilon;
        }

        /// <summary>
        /// Проверка равенства float с учетом точности
        /// </summary>
        public static bool Approximately(float a, float b, float epsilon = 0.0001f)
        {
            return Mathf.Abs(a - b) < epsilon;
        }

        /// <summary>
        /// Вычисление факториала
        /// </summary>
        public static long Factorial(int n)
        {
            if (n <= 1) return 1;
            long result = 1;
            for (int i = 2; i <= n; i++)
            {
                result *= i;
            }
            return result;
        }

        /// <summary>
        /// Вычисление биномиального коэффициента (n выбираем k)
        /// </summary>
        public static long BinomialCoefficient(int n, int k)
        {
            if (k > n) return 0;
            if (k == 0 || k == n) return 1;
            
            k = Mathf.Min(k, n - k);
            long result = 1;
            
            for (int i = 1; i <= k; i++)
            {
                result *= (n - k + i);
                result /= i;
            }
            
            return result;
        }

        /// <summary>
        /// Перестановка элементов массива
        /// </summary>
        public static T[] ShuffleArray<T>(T[] array, System.Random rng)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
            return array;
        }

        /// <summary>
        /// Выбор случайного элемента из списка
        /// </summary>
        public static T RandomChoice<T>(List<T> list)
        {
            if (list == null || list.Count == 0) return default;
            return list[Random.Range(0, list.Count)];
        }

        /// <summary>
        /// Выбор случайного элемента из массива
        /// </summary>
        public static T RandomChoice<T>(T[] array)
        {
            if (array == null || array.Length == 0) return default;
            return array[Random.Range(0, array.Length)];
        }

        /// <summary>
        /// Проверка, является ли число степенью двойки
        /// </summary>
        public static bool IsPowerOfTwo(int x)
        {
            return (x & (x - 1)) == 0;
        }

        /// <summary>
        /// Округление до ближайшей степени двойки
        /// </summary>
        public static int RoundToNextPowerOfTwo(int x)
        {
            if (x <= 1) return 1;
            x--;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x + 1;
        }

        /// <summary>
        /// Конвертация из Unicode символа в int
        /// </summary>
        public static int CharToInt(char c)
        {
            return c - '0';
        }

        /// <summary>
        /// Конвертация из int в Unicode символ
        /// </summary>
        public static char IntToChar(int i)
        {
            return (char)(i + '0');
        }
    }
}