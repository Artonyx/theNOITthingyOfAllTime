using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class SmokeManager : MonoBehaviour
{
    public static SmokeManager Instance { get; private set; }

    [Header("Smoke Sprite")]
    public Sprite smokeSprite;

    [Header("Sorting")]
    [Tooltip("Sorting layer for smoke — should be above buildings.")]
    public string smokeSortingLayer = "Trees";
    [Tooltip("Order in layer for smoke.")]
    public int smokeSortingOrder = 0;

    [Header("Tilemap Reference (for cell centering)")]
    public Tilemap referenceTilemap;

    [Header("Emission by Stage")]
    public float emissionRateSmall  = 3f;
    public float emissionRateMedium = 8f;
    public float emissionRateLarge  = 18f;

    [Header("Particle Settings")]
    public float startSpeed    = 0.5f;
    public float startSize     = 0.6f;
    public float startLifetime = 2.5f;
    [Tooltip("Smoke rises upward — this controls how fast.")]
    public float riseSpeed     = 0.3f;
    [Tooltip("How much smoke drifts sideways randomly.")]
    public float drift         = 0.1f;
    public Color smokeColor    = new Color(0.3f, 0.3f, 0.3f, 0.5f);


    private Dictionary<Vector3Int, ParticleSystem> _smokeParticles
        = new Dictionary<Vector3Int, ParticleSystem>();


    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void UpdateSmoke(Vector3Int cell, FireManager.FireStage stage)
    {
        if (!_smokeParticles.TryGetValue(cell, out ParticleSystem ps) || ps == null)
            ps = SpawnSmoke(cell);

        SetEmissionRate(ps, stage);
    }

    public void RemoveSmoke(Vector3Int cell)
    {
        if (!_smokeParticles.TryGetValue(cell, out ParticleSystem ps)) return;

        if (ps != null)
        {
            var emission = ps.emission;
            emission.enabled = false;
            Destroy(ps.gameObject, ps.main.startLifetime.constant + 0.5f);
        }

        _smokeParticles.Remove(cell);
    }

    private ParticleSystem SpawnSmoke(Vector3Int cell)
    {
        Vector3 worldPos = referenceTilemap != null
            ? referenceTilemap.GetCellCenterWorld(cell)
            : new Vector3(cell.x, cell.y, 0f);

        worldPos.y += 0.3f;

        GameObject go = new GameObject($"Smoke_{cell.x}_{cell.y}");
        go.transform.SetParent(transform, false);
        go.transform.position = worldPos;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        var main           = ps.main;
        main.loop          = true;
        main.playOnAwake   = true;
        main.startLifetime = startLifetime;
        main.startSpeed    = startSpeed;
        main.startSize     = startSize;
        main.startColor    = smokeColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles  = 50;

        var emission       = ps.emission;
        emission.enabled   = true;
        emission.rateOverTime = emissionRateSmall;

        var shape          = ps.shape;
        shape.enabled      = true;
        shape.shapeType    = ParticleSystemShapeType.Circle;
        shape.radius       = 0.1f;

        var vel            = ps.velocityOverLifetime;
        vel.enabled        = true;
        vel.space          = ParticleSystemSimulationSpace.World;
        vel.x              = new ParticleSystem.MinMaxCurve(-drift, drift);
        vel.y              = new ParticleSystem.MinMaxCurve(riseSpeed, riseSpeed * 1.5f);
        vel.z              = new ParticleSystem.MinMaxCurve(0f);

        var sizeOverLife   = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve growCurve = new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(1f, 1.5f));
        sizeOverLife.size  = new ParticleSystem.MinMaxCurve(1f, growCurve);

        var colorOverLife  = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient gradient  = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]  { new GradientColorKey(Color.grey, 0f),
                                      new GradientColorKey(Color.grey, 1f) },
            new GradientAlphaKey[]  { new GradientAlphaKey(smokeColor.a, 0f),
                                      new GradientAlphaKey(0f,           1f) });
        colorOverLife.color = gradient;

        var renderer              = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode       = ParticleSystemRenderMode.Billboard;
        renderer.sortingLayerName = smokeSortingLayer;
        renderer.sortingOrder     = smokeSortingOrder;

        if (smokeSprite != null)
        {
            Material mat      = new Material(Shader.Find("Sprites/Default"));
            mat.mainTexture   = smokeSprite.texture;
            mat.SetInt("_SrcBlend",  (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend",  (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite",    0);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue      = 3000;
            renderer.material    = mat;
        }
        else
        {
            renderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        _smokeParticles[cell] = ps;
        return ps;
    }

    private void SetEmissionRate(ParticleSystem ps, FireManager.FireStage stage)
    {
        var emission = ps.emission;
        emission.rateOverTime = stage switch
        {
            FireManager.FireStage.Small  => emissionRateSmall,
            FireManager.FireStage.Medium => emissionRateMedium,
            FireManager.FireStage.Large  => emissionRateLarge,
            _                            => emissionRateSmall
        };
    }
}