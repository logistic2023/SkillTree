using DG.Tweening;
using UnityEngine;

/// <summary>
/// Add to the Totem prefab.
/// Holds a reference to a child ParticleSystem and plays it
/// whenever a GoldCoin arrives. Also does a quick DOTween scale punch.
///
/// Setup:
///   1. Create an empty child GameObject on the Totem called "GoldCollectFX"
///   2. Add a ParticleSystem to it (configure however you like — burst, sparkles, etc.)
///      Set "Play On Awake" to FALSE.
///   3. Assign that child PS to the 'collectParticles' field on this component.
/// </summary>
public class TotemGoldCollectFX : MonoBehaviour
{
    [Header("Particles")]
    [Tooltip("Child ParticleSystem on the totem. Play On Awake must be OFF.")]
    public ParticleSystem collectParticles;

    [Header("Scale Punch")]
    [Tooltip("Punch scale amount — how much the totem 'pops' on collect.")]
    public Vector3 punchScale = new Vector3(0.15f, 0.15f, 0.15f);

    [Tooltip("Duration of the scale punch.")]
    public float punchDuration = 0.25f;

    [Tooltip("DOTween punch vibrato.")]
    public int punchVibrato = 5;

    [Tooltip("DOTween punch elasticity.")]
    [Range(0f, 1f)]
    public float punchElasticity = 0.5f;

    [Header("Optional: Visual Transform to Punch")]
    [Tooltip("If null, punches this GameObject's transform. " +
             "Assign a child visual mesh if you don't want to scale the collider.")]
    public Transform visualTransform;

    private Transform _punchTarget;
    private Tween     _activePunch;

    private void Awake()
    {
        _punchTarget = visualTransform != null ? visualTransform : transform;
    }

    /// <summary>
    /// Called by GoldCoin when it arrives at this totem.
    /// </summary>
    public void PlayCollectEffect()
    {
        PlayParticles();
        PlayScalePunch();
    }

    private void PlayParticles()
    {
        if (collectParticles == null) return;

        // Stop first so rapid collects always restart cleanly
        collectParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        collectParticles.Play();
    }

    private void PlayScalePunch()
    {
        // Kill any in-progress punch so rapid collects don't stack weirdly
        _activePunch?.Kill(complete: true);

        _activePunch = _punchTarget
            .DOPunchScale(punchScale, punchDuration, punchVibrato, punchElasticity)
            .SetUpdate(UpdateType.Normal);
        
        
    }
}