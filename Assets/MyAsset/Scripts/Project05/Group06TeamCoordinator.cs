using UnityEngine;
using UnityEngine.AI;

namespace Project05
{


    namespace Group06
    {
        public class Group06TeamCoordinator : MonoBehaviour
        {
            [SerializeField] private GameObject chara1;
            [SerializeField] private GameObject chara2;

            private Group06Player[] agents = new Group06Player[2];
            private bool[] registered = new bool[2];

            private int n; // targets count
            private Vector3[] tp; // target positions
            private float[,] dist; // cached navmesh path length (node x node)

            // node index: 0 = agent0 start, 1 = agent1 start, 2..n+1 = targets
            private int[][] plan = { new int[0], new int[0] };
            private int[] planPos = { 0, 0 };

            private NavMeshPath path0, path1; // reuse

            public bool IsActive { get; private set; }

            void Awake()
            {
                path0 = new NavMeshPath();
                path1 = new NavMeshPath();
            }

            void Start()
            {
                // ターゲット情報キャッシュ
                n = GameManager_Project05.instance.NumTargets();
                tp = new Vector3[n];
                for (int i = 0; i < n; i++) tp[i] = GameManager_Project05.instance.TargetPosition(i);

                // Playerの付与（Chara_2にも確実に付ける）
                EnsurePlayer(chara1);
                EnsurePlayer(chara2);
            }

            void Update()
            {
                TryRegister(0, chara1);
                TryRegister(1, chara2);

                if (!registered[0] || !registered[1])
                {
                    IsActive = false;
                    return;
                }

                IsActive = true;

                // dist未作成なら一回だけ作る（開始直後に作ればOK）
                if (dist == null)
                {
                    BuildDistanceTable();
                    ReplanAll(); // 最初のルート決定
                }

                // ルートが尽きてるなら何もしない
            }

            private void EnsurePlayer(GameObject go)
            {
                if (go == null) return;
                if (go.GetComponent<Group06Player>() == null)
                    go.AddComponent<Group06Player>();
            }

            private void TryRegister(int id, GameObject go)
            {
                if (registered[id] || go == null) return;
                var p = go.GetComponent<Group06Player>();
                if (p == null) return;

                agents[id] = p;
                p.SetCoordinator(this, id);
                registered[id] = true;
            }

            private void BuildDistanceTable()
            {
                int nodes = n + 2;
                dist = new float[nodes, nodes];

                Vector3 a0 = agents[0].GetPosition();
                a0.y = 0;
                Vector3 a1 = agents[1].GetPosition();
                a1.y = 0;

                // start -> target
                for (int t = 0; t < n; t++)
                {
                    Vector3 g = tp[t];
                    g.y = 0;
                    dist[0, 2 + t] = CalcPathLen(a0, g, path0);
                    dist[1, 2 + t] = CalcPathLen(a1, g, path1);
                }

                // target -> target
                for (int i = 0; i < n; i++)
                {
                    Vector3 si = tp[i];
                    si.y = 0;
                    for (int j = 0; j < n; j++)
                    {
                        if (i == j)
                        {
                            dist[2 + i, 2 + j] = 0f;
                            continue;
                        }

                        Vector3 gj = tp[j];
                        gj.y = 0;
                        dist[2 + i, 2 + j] = CalcPathLen(si, gj, path0);
                    }
                }
            }

            private float CalcPathLen(Vector3 start, Vector3 goal, NavMeshPath p)
            {
                if (!NavMesh.CalculatePath(start, goal, NavMesh.AllAreas, p)) return float.PositiveInfinity;
                if (p.status != NavMeshPathStatus.PathComplete) return float.PositiveInfinity;

                float total = 0f;
                var corners = p.corners; // 小規模なので許容
                for (int i = 0; i < corners.Length - 1; i++)
                    total += (corners[i + 1] - corners[i]).magnitude;
                return total;
            }

            // ===== ルート計画：max(距離) を最小化 =====
            private void ReplanAll()
            {
                int all = 0;
                UpdateStartToTargetDistances();
                for (int t = 0; t < n; t++)
                    if (GameManager_Project05.instance.IsTargetActive(t))
                        all |= (1 << t);

                if (all == 0)
                {
                    plan[0] = new int[0];
                    plan[1] = new int[0];
                    planPos[0] = planPos[1] = 0;
                    return;
                }

                float best = float.PositiveInfinity;
                int bestMask = 0;
                int[] bestOrder0 = null;
                int[] bestOrder1 = null;

                for (int mask0 = 0; mask0 <= all; mask0++)
                {
                    int mask1 = all ^ mask0;

                    var r0 = SolveBestOrder(startNode: 0, maskTargets: mask0);
                    var r1 = SolveBestOrder(startNode: 1, maskTargets: mask1);

                    // 到達不能が混じる場合は弾く
                    if (float.IsInfinity(r0.cost) || float.IsInfinity(r1.cost)) continue;

                    float makespan = Mathf.Max(r0.cost, r1.cost);
                    if (makespan < best)
                    {
                        best = makespan;
                        bestMask = mask0;
                        bestOrder0 = r0.order;
                        bestOrder1 = r1.order;
                    }
                }

                plan[0] = bestOrder0 ?? new int[0];
                plan[1] = bestOrder1 ?? new int[0];
                planPos[0] = 0;
                planPos[1] = 0;
            }

            private void UpdateStartToTargetDistances()
            {
                Vector3 a0 = agents[0].GetPosition();
                a0.y = 0;
                Vector3 a1 = agents[1].GetPosition();
                a1.y = 0;

                for (int t = 0; t < n; t++)
                {
                    Vector3 g = tp[t];
                    g.y = 0;
                    dist[0, 2 + t] = CalcPathLen(a0, g, path0);
                    dist[1, 2 + t] = CalcPathLen(a1, g, path1);
                }
            }

            private (float cost, int[] order) SolveBestOrder(int startNode, int maskTargets)
            {
                // maskTargets は targets(0..n-1) のビット
                if (maskTargets == 0) return (0f, new int[0]);

                int mCount = CountBits(maskTargets);
                // 対象ターゲットを配列に詰める（DPのインデックスを詰める）
                int[] idx = new int[mCount];
                int k = 0;
                for (int t = 0; t < n; t++)
                    if ((maskTargets & (1 << t)) != 0)
                        idx[k++] = t;

                int M = mCount;
                int size = 1 << M;

                // dp[mask,last] を1次元に詰める
                float[] dp = new float[size * M];
                int[] parent = new int[size * M];

                for (int i = 0; i < dp.Length; i++)
                {
                    dp[i] = float.PositiveInfinity;
                    parent[i] = -1;
                }

                // base
                for (int i = 0; i < M; i++)
                {
                    float d = dist[startNode, 2 + idx[i]];
                    dp[(1 << i) * M + i] = d;
                }

                // trans
                for (int mask = 1; mask < size; mask++)
                {
                    for (int last = 0; last < M; last++)
                    {
                        if ((mask & (1 << last)) == 0) continue;
                        float cur = dp[mask * M + last];
                        if (float.IsInfinity(cur)) continue;

                        for (int nxt = 0; nxt < M; nxt++)
                        {
                            if ((mask & (1 << nxt)) != 0) continue;
                            int nmask = mask | (1 << nxt);

                            float add = dist[2 + idx[last], 2 + idx[nxt]];
                            float v = cur + add;
                            int pos = nmask * M + nxt;

                            if (v < dp[pos])
                            {
                                dp[pos] = v;
                                parent[pos] = last;
                            }
                        }
                    }
                }

                // best end
                int full = size - 1;
                float best = float.PositiveInfinity;
                int bestLast = -1;
                for (int last = 0; last < M; last++)
                {
                    float v = dp[full * M + last];
                    if (v < best)
                    {
                        best = v;
                        bestLast = last;
                    }
                }

                if (bestLast < 0) return (float.PositiveInfinity, new int[0]);

                // reconstruct order (reverse)
                int[] order = new int[M];
                int curMask = full;
                int curLast = bestLast;
                for (int i = M - 1; i >= 0; i--)
                {
                    order[i] = idx[curLast];
                    int prev = parent[curMask * M + curLast];
                    curMask ^= 1 << curLast;
                    curLast = prev;
                    if (curMask == 0) break;
                }

                return (best, order);
            }

            public void RejectTarget(int agentId, int targetIndex)
            {
                // 今の方式だと「予約」という概念はないので、
                // 詰んだターゲットを飛ばして次に進ませるだけ。
                if (agentId < 0 || agentId > 1) return;

                // 該当ターゲットが次に狙うやつならスキップ
                if (planPos[agentId] < plan[agentId].Length &&
                    plan[agentId][planPos[agentId]] == targetIndex)
                {
                    planPos[agentId]++;
                }

                // 念のため再計画
                ReplanAll();
            }

            private int CountBits(int x)
            {
                int c = 0;
                while (x != 0)
                {
                    x &= x - 1;
                    c++;
                }

                return c;
            }

            // ===== Player側API =====
            public int GetNextTarget(int agentId)
            {
                if (agentId < 0 || agentId > 1) return -1;

                // 既に取られてる爆弾は飛ばす
                while (planPos[agentId] < plan[agentId].Length)
                {
                    int t = plan[agentId][planPos[agentId]];
                    if (GameManager_Project05.instance.IsTargetActive(t)) return t;
                    planPos[agentId]++;
                }

                return -1;
            }

            public Vector3 GetTargetPosition(int index) => (index < 0 || index >= n) ? Vector3.zero : tp[index];

            public bool IsTargetStillActive(int index) => GameManager_Project05.instance.IsTargetActive(index);

            public void NotifyTargetHit(int agentId, int targetIndex)
            {
                // その爆弾まで進んだ扱いにする
                if (agentId < 0 || agentId > 1) return;
                if (planPos[agentId] < plan[agentId].Length && plan[agentId][planPos[agentId]] == targetIndex)
                    planPos[agentId]++;

                // ★ここが効く：爆弾が減ったら再計画（回数は爆弾数分だけ）
                ReplanAll();
            }
        }
    }
}
