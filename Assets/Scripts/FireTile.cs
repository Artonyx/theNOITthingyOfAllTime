using System.Collections;
using UnityEngine;

/// <summary>
/// Represents a single burning tile with three escalating burn stages:
///   Small -> Medium -> Large
///
/// Escalation:
///   The fire automatically escalates over time using configurable delays.
///
/// Spreading:
///   Only begins spreading at Medium. Large spreads faster and more aggressively.
///
/// Extinguishing (called by FireManager.ExtinguishNearest via the firetruck):
///   Each Extinguish() call reduces the stage by one level:
///     Large  -> Medium  (escalation halted; won't re-grow)
///     Medium -> Small
///     Small  -> fully gone (tile destroyed after a short smoke delay)
///
/// Visual hookup:
///   Assign three child GameObjects (one per stage) in the Inspector.
///   The script activates only the correct one at any given time.
/// </summary>
public class FireTile : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Burn stage
    // -------------------------------------------------------------------------

    public enum BurnStage { Small = 0, Medium = 1, Large = 2 }

    // -------------------------------------------------------------------------
    // Inspector -- Stage Visuals
    // -------------------------------------------------------------------------

    [Header("Stage Visuals")]
    [Tooltip("Child GameObject shown at Small stage.")]
    public GameObject smallFireVisual;

    [Tooltip("Child GameObject shown at Medium stage.")]
    public GameObject mediumFireVisual;

    [Tooltip("Child GameObject shown at Large stage.")]
    public GameObject largeFireVisual;

    // -------------------------------------------------------------------------
    // Inspector -- Escalation
    // -------------------------------------------------------------------------

    [Header("Escalation Timings")]
    [Tooltip("Seconds at Small before escalating to Medium.")]
    [Range(1f, 60f)] public float smallToMediumTime = 8f;

    [Tooltip("Seconds at Medium before escalating to Large.")]
    [Range(1f, 60f)] public float mediumToLargeTime = 10f;

    // -------------------------------------------------------------------------
    // Inspector -- Spread
    // -------------------------------------------------------------------------

    [Header("Spread Settings")]
    [Tooltip("Seconds between spread attempts at Medium stage.")]
    [Range(0.5f, 30f)] public float spreadIntervalMedium = 12f;

    [Tooltip("Seconds between spread attempts at Large stage.")]
    [Range(0.5f, 30f)] public float spreadIntervalLarge = 4f;

    [Tooltip("Maximum number of times this tile spreads before stopping.")]
    [Range(1, 10)] public int maxSpreads = 4;

    [Tooltip("Probability a Medium spread attempt succeeds.")]
    [Range(0f, 1f)] public float spreadChanceMedium = 0.3f;

    [Tooltip("Probability a Large spread attempt succeeds.")]
    [Range(0f, 1f)] public float spreadChanceLarge = 0.7f;

    // -------------------------------------------------------------------------
    // Inspector -- Audio (optional)
    // -------------------------------------------------------------------------

    [Header("Audio (optional)")]
    [Tooltip("Looping crackle AudioSource. Pitch rises with each stage.")]
    public AudioSource fireCrackle;

    [Tooltip("Pitch values for Small, Medium, and Large stages.")]
    public float[] stagePitches = { 0.8f, 1.0f, 1.3f };

    // -------------------------------------------------------------------------
    // Runtime state (read from other scripts if needed)
    // -------------------------------------------------------------------------

    public Vector2Int GridPosition { get; set; }
    public BurnStage  CurrentStage { get; private set; } = BurnStage.Small;
    public bool       IsExtinguished { get; private set; } = false;

    // -------------------------------------------------------------------------
    // Private
    // -------------------------------------------------------------------------

    private int      spreadsRemaining;
    private Coroutine escalationCoroutine;
    private Coroutine spreadCoroutine;

    // Instance array so ShuffleDirections() doesn't affect other FireTiles.
    private Vector2Int[] directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        spreadsRemaining    = maxSpreads;
        escalationCoroutine = StartCoroutine(EscalationRoutine());
        spreadCoroutine     = StartCoroutine(SpreadRoutine());

        ApplyStageVisuals(BurnStage.Small);

        if (fireCrackle != null && !fireCrackle.isPlaying)
            fireCrackle.Play();
    }

    // -------------------------------------------------------------------------
    // Escalation
    // -------------------------------------------------------------------------

    private IEnumerator EscalationRoutine()
    {
        yield return new WaitForSeconds(smallToMediumTime);
        if (IsExtinguished) yield break;
        ChangeStage(BurnStage.Medium);

        yield return new WaitForSeconds(mediumToLargeTime);
        if (IsExtinguished) yield break;
        ChangeStage(BurnStage.Large);
        // Large is the final stage.
    }

    private void ChangeStage(BurnStage newStage)
    {
        if (IsExtinguished) return;
        CurrentStage = newStage;
        ApplyStageVisuals(newStage);
    }

    private void ApplyStageVisuals(BurnStage stage)
    {
        if (smallFireVisual  != null) smallFireVisual.SetActive(stage  == BurnStage.Small);
        if (mediumFireVisual != null) mediumFireVisual.SetActive(stage == BurnStage.Medium);
        if (largeFireVisual  != null) largeFireVisual.SetActive(stage  == BurnStage.Large);

        if (fireCrackle != null && stagePitches.Length > (int)stage)
            fireCrackle.pitch = stagePitches[(int)stage];
    }

    // -------------------------------------------------------------------------
    // Spread logic
    // -------------------------------------------------------------------------

    private IEnumerator SpreadRoutine()
    {
        // Wait until we leave Small stage before spreading.
        while (CurrentStage == BurnStage.Small && !IsExtinguished)
            yield return new WaitForSeconds(1f);

        while (spreadsRemaining > 0 && !IsExtinguished)
        {
            bool  isLarge  = CurrentStage == BurnStage.Large;
            float interval = isLarge ? spreadIntervalLarge  : spreadIntervalMedium;
            float chance   = isLarge ? spreadChanceLarge    : spreadChanceMedium;

            yield return new WaitForSeconds(interval);
            if (IsExtinguished) yield break;

            TrySpread(chance);
            spreadsRemaining--;
        }
    }

    private void TrySpread(float chance)
    {
        ShuffleDirections();

        foreach (var dir in directions)
        {
            if (Random.value > chance) continue;

            Vector2Int target = GridPosition + dir;

            if (FireManager.Instance != null && !FireManager.Instance.IsBurning(target))
            {
                FireManager.Instance.SpawnFire(new Vector2(target.x, target.y));
                break; // one neighbour per tick; remove 'break' to allow multi-spread
            }
        }
    }

    // -------------------------------------------------------------------------
    // Extinguishing -- called by FireManager.ExtinguishNearest()
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reduces the fire by ONE stage per call.
    ///   Large  -> Medium  (escalation stops; won't re-grow)
    ///   Medium -> Small
    ///   Small  -> fully extinguished and destroyed
    /// </summary>
    public void Extinguish()
    {
        if (IsExtinguished) return;

        switch (CurrentStage)
        {
            case BurnStage.Large:
                // Stop the escalation coroutine so it stays at Medium after being knocked down.
                if (escalationCoroutine != null)
                {
                    StopCoroutine(escalationCoroutine);
                    escalationCoroutine = null;
                }
                ChangeStage(BurnStage.Medium);
                break;

            case BurnStage.Medium:
                ChangeStage(BurnStage.Small);
                break;

            case BurnStage.Small:
                FullyExtinguish();
                break;
        }
    }

    /// <summary>Instantly destroys the fire regardless of stage. Use for resets or power-ups.</summary>
    public void ForceExtinguish()
    {
        if (!IsExtinguished) FullyExtinguish();
    }

    private void FullyExtinguish()
    {
        IsExtinguished = true;

        if (escalationCoroutine != null) StopCoroutine(escalationCoroutine);
        if (spreadCoroutine     != null) StopCoroutine(spreadCoroutine);

        if (smallFireVisual  != null) smallFireVisual.SetActive(false);
        if (mediumFireVisual != null) mediumFireVisual.SetActive(false);
        if (largeFireVisual  != null) largeFireVisual.SetActive(false);

        if (fireCrackle != null) fireCrackle.Stop();

        // Notify FireManager to remove from registry.
        FireManager.Instance?.UnregisterFire(GridPosition);

        // Short delay allows a smoke-puff VFX to finish before the GameObject is destroyed.
        Destroy(gameObject, 1.5f);
    }

    // -------------------------------------------------------------------------
    // Firetruck proximity helper
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true if the firetruck is within extinguishing range.
    /// Used by UIManager to show/hide the Extinguish button.
    /// </summary>
    public bool IsWithinExtinguishRange(Vector2 firetruckWorldPos, float range = 1.5f)
        => Vector2.Distance(transform.position, firetruckWorldPos) <= range;

    // -------------------------------------------------------------------------
    // Utility
    // -------------------------------------------------------------------------

    private void ShuffleDirections()
    {
        for (int i = directions.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (directions[i], directions[j]) = (directions[j], directions[i]);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = CurrentStage switch
        {
            BurnStage.Small  => new Color(1f, 1f,  0f,  0.4f),
            BurnStage.Medium => new Color(1f, 0.5f, 0f, 0.4f),
            BurnStage.Large  => new Color(1f, 0.1f, 0f, 0.5f),
            _                => Color.white
        };
        Gizmos.DrawWireCube(transform.position, Vector3.one);
    }
}