using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class EnemyFloorTransition : MonoBehaviour
{
    [SerializeField] Transform exit;
    [SerializeField] float transitionDuration = 2f;

    bool isTransitioning = false;

    void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isTransitioning) return;
        if (!other.CompareTag("Enemy")) return;

        GuardAI guard = other.GetComponent<GuardAI>();
        if (guard == null) return;

        StartCoroutine(TransitionSequence(other.gameObject, guard));
    }

    IEnumerator TransitionSequence(GameObject enemyGO, GuardAI guard)
    {
        isTransitioning = true;
        
        // Замораживаем стражника
        guard.IsTraversing = true;
        
        yield return new WaitForSeconds(transitionDuration);
        
        // Телепорт
        enemyGO.transform.position = exit.position;
        
        // Размораживаем
        guard.IsTraversing = false;
        isTransitioning = false;
    }
}
