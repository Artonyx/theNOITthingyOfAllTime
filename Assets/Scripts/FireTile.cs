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
///   Only begins spreading at Medium (configurable). Large spreads faster.
///
/// Extinguishing (called by your firetruck button):
///   Each Extinguish() call reduces the stage by one level:
///     Large  -> Medium
///     Medium -> Small
///     Small  -> fully gone (tile destroyed)
///   After being knocked down, the fire does NOT re-escalate.
///
/// Visual hookup:
///   Assign three child GameObjects (one per stage) in the Inspector.
///   The script activates only the correct one at any given time.
/// </summary>
public class FireTile : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Burn stage enum
    // -------------------------------------------------------------------------

    public enum BurnStage { Small = 0, Medium = 1, Large = 2 }

    // -------------------------------------------------------------------------
    // Inspector -- Stage Visuals
    // -------------------------------------------------------------------------

    [Header("Stage Visuals")]
    [Tooltip("Child GameObject shown at Small stage (small fire sprite / particles).")]
    public GameObject smallFireVisual;

    [Tooltip("Child GameObject shown at Medium stage.")]
    public GameObject mediumFireVisual;

    [Tooltip("Child GameObject shown at Large stage.")]
    public GameObject largeFireVisual;

    // -------------------------------------------------------------------------
    // Inspector -- Escalation
    // -------------------------------------------------------------------------

    [Header("Escalation Timings")]
    [Tooltip("Seconds at Small stage before escalating to Medium.")]
    [Range(1f, 60f)] public float smallToMediumTime = 8f;

    [Tooltip("Seconds at Medium stage before escalating to Large.")]
    [Range(1f, 60f)] public float mediumToLargeTime = 10f;

    // -------------------------------------------------------------------------
    // Inspector -- Spread Settings
    // -------------------------------------------------------------------------

    [Header("Spread Settings")]
    [Tooltip("Seconds between spread attempts at Medium stage (slow).")]
    [Range(0.5f, 30f)] public float spreadIntervalMedium = 12f;

    [Tooltip("Seconds between spread attempts at Large stage (faster).")]
    [Range(0.5f, 30f)] public float spreadIntervalLarge = 4f;

    [Tooltip("Maximum total times this tile spreads to neighbours before stopping.")]
    [Range(1, 10)] public int maxSpreads = 4;

    [Tooltip("Probability (0-1) that a Medium-stage spread attempt succeeds. Keep this lower than Large.")]
    [Range(0f, 1f)] public float spreadChanceMedium = 0.3f;

    [Tooltip("Probability (0-1) that a Large-stage spread attempt succeeds.")]
    [Range(0f, 1f)] public float spreadChanceLarge = 0.7f;

    // -------------------------------------------------------------------------
    // Inspector -- Audio (optional)
    // -------------------------------------------------------------------------

    [Header("Audio (optional)")]
    [Tooltip("Looping crackle AudioSource. Pitch rises with each stage.")]
    public AudioSource fireCrackle;

    [Tooltip("Pitch values for Small, Medium, and Large stages respectively.")]
    public float[] stagePitches = { 0.8f, 1.0f, 1.3f };

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    public Vector2Int GridPosition { get; set; }
    public BurnStage CurrentStage { get; private set; } = BurnStage.Small;
    public bool IsExtinguished { get; private set; } = false;

    // -------------------------------------------------------------------------
    // Private
    // -------------------------------------------------------------------------

    private int spreadsRemaining;
    private Coroutine escalationCoroutine;
    private Coroutine spreadCoroutine;

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        spreadsRemaining = maxSpreads;
        escalationCoroutine = StartCoroutine(EscalationRoutine());
        spreadCoroutine = StartCoroutine(SpreadRoutine());

        ApplyStageVisuals(BurnStage.Small);

        if (fireCrackle != null && !fireCrackle.isPlaying)
            fireCrackle.Play();
    }

    // -------------------------------------------------------------------------
    // Escalation
    // -------------------------------------------------------------------------

    private IEnumerator EscalationRoutine()
    {
        // Small -> Medium
        yield return new WaitForSeconds(smallToMediumTime);
        if (IsExtinguished) yield break;
        ChangeStage(BurnStage.Medium);

        // Medium -> Large
        yield return new WaitForSeconds(mediumToLargeTime);
        if (IsExtinguished) yield break;
        ChangeStage(BurnStage.Large);

        // Large is final; no further escalation.
    }

    private void ChangeStage(BurnStage newStage)
    {
        if (IsExtinguished) return;
        CurrentStage = newStage;
        ApplyStageVisuals(newStage);
    }

    private void ApplyStageVisuals(BurnStage stage)
    {
        if (smallFireVisual != null) smallFireVisual.SetActive(stage == BurnStage.Small);
        if (mediumFireVisual != null) mediumFireVisual.SetActive(stage == BurnStage.Medium);
        if (largeFireVisual != null) largeFireVisual.SetActive(stage == BurnStage.Large);

        if (fireCrackle != null && stagePitches.Length > (int)stage)
            fireCrackle.pitch = stagePitches[(int)stage];
    }

    // -------------------------------------------------------------------------
    // Spread logic
    // -------------------------------------------------------------------------

    private IEnumerator SpreadRoutine()
    {
        // Small stage never spreads — wait until we reach Medium first.
        while (CurrentStage == BurnStage.Small && !IsExtinguished)
            yield return new WaitForSeconds(1f);

        while (spreadsRemaining > 0 && !IsExtinguished)
        {
            // Pick interval and success chance based on current stage.
            // Large is faster and more likely to infect than Medium.
            bool isLarge = CurrentStage == BurnStage.Large;
            float interval = isLarge ? spreadIntervalLarge : spreadIntervalMedium;
            float chance = isLarge ? spreadChanceLarge : spreadChanceMedium;

            yield return new WaitForSeconds(interval);
            if (IsExtinguished) yield break;

            TrySpread(chance);
            spreadsRemaining--;
        }
    }

    private void TrySpread(float chance)
    {
        ShuffleDirections();

        foreach (var dir in Directions)
        {
            if (Random.value > chance) continue;

            Vector2Int target = GridPosition + dir;

            if (FireManager.Instance != null && !FireManager.Instance.IsBurning(target))
            {
                FireManager.Instance.SpawnFire(new Vector2(target.x, target.y));
                break; // Spread to ONE neighbour per tick. Remove 'break' for multi-spread.
            }
        }
    }

    // -------------------------------------------------------------------------
    // Extinguishing -- called by your firetruck button script
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reduces the fire by ONE stage per call.
    ///
    ///   Large  -> Medium  (escalation stopped; fire won't grow back)
    ///   Medium -> Small
    ///   Small  -> fully extinguished and destroyed
    ///
    /// Wire this up in your firetruck button handler:
    ///   FireTile fire = GetNearestFire();
    ///   if (fire != null) fire.Extinguish();
    /// </summary>
    public void Extinguish()
    {
        if (IsExtinguished) return;

        switch (CurrentStage)
        {
            case BurnStage.Large:
                // Stop auto-escalation so the tile stays at Medium after being knocked down.
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

    /// <summary>
    /// Instantly destroys the fire regardless of current stage.
    /// Useful for power-ups, level resets, or debug tools.
    /// </summary>
    public void ForceExtinguish()
    {
        if (!IsExtinguished) FullyExtinguish();
    }

    private void FullyExtinguish()
    {
        IsExtinguished = true;

        if (escalationCoroutine != null) StopCoroutine(escalationCoroutine);
        if (spreadCoroutine != null) StopCoroutine(spreadCoroutine);

        if (smallFireVisual != null) smallFireVisual.SetActive(false);
        if (mediumFireVisual != null) mediumFireVisual.SetActive(false);
        if (largeFireVisual != null) largeFireVisual.SetActive(false);

        if (fireCrackle != null) fireCrackle.Stop();

        FireManager.Instance?.UnregisterFire(GridPosition);

        // Short delay so a smoke-puff effect has time to play before destruction.
        Destroy(gameObject, 1.5f);
    }

    // -------------------------------------------------------------------------
    // Firetruck proximity helper
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true if the firetruck world position is within extinguishing range.
    /// Use this in your firetruck script to show / hide the extinguish button.
    /// </summary>
    public bool IsWithinExtinguishRange(Vector2 firetruckWorldPos, float range = 1.5f)
    {
        return Vector2.Distance(transform.position, firetruckWorldPos) <= range;
    }

    // -------------------------------------------------------------------------
    // Utility
    // -------------------------------------------------------------------------

    private void ShuffleDirections()
    {
        for (int i = Directions.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (Directions[i], Directions[j]) = (Directions[j], Directions[i]);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = CurrentStage switch
        {
            BurnStage.Small => new Color(1f, 1f, 0f, 0.4f),
            BurnStage.Medium => new Color(1f, 0.5f, 0f, 0.4f),
            BurnStage.Large => new Color(1f, 0.1f, 0f, 0.5f),
            _ => Color.white
        };
        Gizmos.DrawWireCube(transform.position, Vector3.one);
    }
}