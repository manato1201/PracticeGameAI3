using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_AI_NAVIGATION
using Unity.AI.Navigation;
#endif

[DefaultExecutionOrder(-1000)]
public sealed class RandomLayoutPlacer : MonoBehaviour
{
    [Header("Assign in Inspector")]
    [SerializeField] private List<Transform> targets = new();
    [SerializeField] private List<Transform> walls = new();
    [SerializeField] private List<Transform> spawnPoints = new();

    [Header("Options")]
    [Tooltip("開始時の乱数シード。-1なら毎回ランダム")]
    [SerializeField] private int seed = -1;

    [Header("Fixed Y")]
    [SerializeField] private float wallY = 0.5f;
    [SerializeField] private float targetY = 0.35f;

    [Header("No overlap (same spawn point)")]
    [Tooltip("壁+ターゲットの総数分だけspawnPointsが必要")]
    [SerializeField] private bool noSamePointOverlap = true;

#if UNITY_AI_NAVIGATION
    [Header("NavMesh (optional)")]
    [SerializeField] private NavMeshSurface[] rebuildSurfaces;
#endif

    private System.Random rng;

    private void Awake()
    {
        int need = targets.Count + walls.Count;
        if (need == 0) return;

        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogError("[RandomLayoutPlacer] spawnPoints が空です。");
            return;
        }

        if (noSamePointOverlap && spawnPoints.Count < need)
        {
            Debug.LogError($"[RandomLayoutPlacer] spawnPoints不足。必要={need} / 現在={spawnPoints.Count}");
            return;
        }

        rng = (seed < 0) ? new System.Random(Guid.NewGuid().GetHashCode()) : new System.Random(seed);

        // シャッフルして使い回しを防ぐ
        var indices = new int[spawnPoints.Count];
        for (int i = 0; i < indices.Length; i++) indices[i] = i;
        Shuffle(indices);

        int cursor = 0;

        // 壁配置
        for (int i = 0; i < walls.Count; i++)
        {
            var w = walls[i];
            if (w == null) continue;

            int spIndex = noSamePointOverlap ? indices[cursor++] : indices[rng.Next(indices.Length)];
            Vector3 p = spawnPoints[spIndex].position;
            p.y = wallY;
            w.position = p;
        }

        // ターゲット配置（壁と同じspIndexにならない）
        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            if (t == null) continue;

            int spIndex = noSamePointOverlap ? indices[cursor++] : indices[rng.Next(indices.Length)];
            Vector3 p = spawnPoints[spIndex].position;
            p.y = targetY;
            t.position = p;
        }

#if UNITY_AI_NAVIGATION
        if (rebuildSurfaces != null && rebuildSurfaces.Length > 0)
        {
            for (int i = 0; i < rebuildSurfaces.Length; i++)
            {
                if (rebuildSurfaces[i] != null) rebuildSurfaces[i].BuildNavMesh();
            }
        }
#endif
    }

    private void Shuffle(int[] a)
    {
        for (int i = a.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (a[i], a[j]) = (a[j], a[i]);
        }
    }
}
