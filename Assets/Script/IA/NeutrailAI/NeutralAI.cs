using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Classe pour les IA neutres (animaux, PNJ, etc.)
/// </summary>
public class NeutralAI : BaseAI
{
    [Header("Paramètres spécifiques Neutral")]
    [SerializeField] private float fleeDistance = 8f; // Distance à laquelle l'IA commence à fuir

    protected override void Awake()
    {
        base.Awake();
        aiType = AIType.Neutral;
    }

    protected override void DetectEnvironment()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, detectedColliders, detectionLayers);

        // Vérifier si un joueur est à proximité pour potentiellement fuir
        for (int i = 0; i < hitCount; i++)
        {
            if (detectedColliders[i].CompareTag("Player"))
            {
                float distanceToPlayer = Vector3.Distance(transform.position, detectedColliders[i].transform.position);

                // Si le joueur est trop proche, fuir
                if (distanceToPlayer < fleeDistance && HasLineOfSight(detectedColliders[i].transform))
                {
                    target = detectedColliders[i].transform;
                    TransitionToState(AIState.Fleeing);
                    return;
                }
            }
        }

        // Si on ne détecte plus le joueur et qu'on était en fuite, revenir à l'état passif
        if (currentState == AIState.Fleeing && (target == null || Vector3.Distance(transform.position, target.position) > fleeDistance * 1.5f))
        {
            TransitionToState(AIState.Passive);
        }
    }
}