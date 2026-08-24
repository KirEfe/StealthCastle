using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // Подключаем систему ввода

public class OneWayPlatform : MonoBehaviour
{
    private PlatformEffector2D _effector;
    [SerializeField] private float disableDelay = 0.3f;

    [Header("Настройка Ввода")]
    [SerializeField] private InputActionReference oneWayActionReference; // Ссылка на наш экшен

    private void Awake()
    {
        _effector = GetComponent<PlatformEffector2D>();
    }

    private void OnEnable()
    {
        // Обязательно включаем экшен при активации платформы
        if (oneWayActionReference != null) 
            oneWayActionReference.action.Enable();
    }

    private void OnDisable()
    {
        if (oneWayActionReference != null)
            oneWayActionReference.action.Disable();
    }

    private void Update()
    {
        // Проверяем, было ли нажатие на любую из кнопок, привязанных к OneWay
        if (oneWayActionReference != null && oneWayActionReference.action.WasPressedThisFrame())
        {
            StartCoroutine(DropThroughRoutine());
        }
    }

    private IEnumerator DropThroughRoutine()
    {
        _effector.rotationalOffset = 180f;
        yield return new WaitForSeconds(disableDelay);
        _effector.rotationalOffset = 0f;
    }
}