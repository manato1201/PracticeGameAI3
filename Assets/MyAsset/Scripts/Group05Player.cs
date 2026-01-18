using UnityEngine;

namespace Group05
{
    public class Group05Player : Pawn
    {
        [Header("Intervals (NO every-frame heavy work)")]
        [SerializeField] private float decisionInterval = 0.2f;
        [SerializeField] private float bombScanInterval = 0.25f;

        [Header("Throw / Combat")]
        [SerializeField] private float maxThrowDistance = 8.5f;     // 遠距離先制
        [SerializeField] private float throwSafetyMargin = 1.5f;    // 自爆しない余裕
        [SerializeField] private float shootLineMaxDistance = 10f;  // 射線チェック上限

        [Header("Evade")]
        [SerializeField] private float bombDetectRadius = 10f;      // 周囲の爆弾を拾う範囲
        [SerializeField] private int evadeSearchDepth = 3;          // 退避探索(2〜3推奨)

        [Header("Anti-Stall (10 seconds same-cell penalty)")]
        [SerializeField] private float stallLimitSeconds = 10f;     // ルール：10秒以上同じ場所で爆弾が来る
        [SerializeField] private float throwFreezeSeconds = 3.5f;   // 投げた後の硬直
        [SerializeField] private float stallSafetyMargin = 0.5f;    // 余裕
        [SerializeField] private float preStallForceMoveAt = 9.0f;  // 9秒で強制1歩

        private Group05Team team;
        private NavMeshTool navi;

        private float nextDecisionTime = -1f;
        private float nextBombScanTime = -1f;

        private const int GridSize = Group05Team.GridMax + 1;
        private readonly bool[,] danger = new bool[GridSize, GridSize];

        // GC抑制
        private readonly Collider[] bombHits = new Collider[64];

        private int selfRole = 0; // 0:攻め 1:回り込み

        // --- anti-stall state ---
        private Vector3Int lastCell;
        private float lastCellChangeTime;
        private bool initializedCell = false;
        private float freezeEndTime = -1f;

        void Start()
        {
            SetGroupNo(5);

            team = GetComponentInParent<Group05Team>();
            navi = GetNavMeshTool();

            int sib = transform.GetSiblingIndex();
            selfRole = (sib == 0) ? 0 : 1;

            nextDecisionTime = Time.time;
            nextBombScanTime = Time.time;

            if (team != null)
            {
                lastCell = team.WorldToCell(GetPosition());
                lastCellChangeTime = Time.time;
                initializedCell = true;
            }
        }

        void Update()
        {
            if (IsDead()) return;

            // セル変化監視（同じ場所に居続けた時間を管理）
            if (team != null)
            {
                Vector3Int nowCell = team.WorldToCell(GetPosition());
                if (!initializedCell)
                {
                    lastCell = nowCell;
                    lastCellChangeTime = Time.time;
                    initializedCell = true;
                }
                else if (nowCell != lastCell)
                {
                    lastCell = nowCell;
                    lastCellChangeTime = Time.time;
                }
            }

            float now = Time.time;

            if (now >= nextBombScanTime)
            {
                RebuildDangerMap();
                nextBombScanTime = now + bombScanInterval;
            }

            if (now < nextDecisionTime) return;
            nextDecisionTime = now + decisionInterval;

            ThinkAndAct();
        }

        private void ThinkAndAct()
        {
            if (team == null) return;

            Vector3 myPos = GetPosition();
            Vector3Int myCell = team.WorldToCell(myPos);

            // --- Anti-stall: 9秒を超えたら何より先に1歩動く（止まる＝爆弾が来るルール対策）
            float sameCellTime = Time.time - lastCellChangeTime;

            if (sameCellTime >= preStallForceMoveAt)
            {
                if (TryForceStepToResetStall(out Vector3 step))
                {
                    MoveToward(myPos, step, 1f);
                    return;
                }
                // それでも動けないなら、とにかく方向を変える（完全停止を避ける）
                MoveDirectionSafe(-GetDirection(), 0.8f);
                return;
            }

            // フリーズ明けはまず1歩動いて「同じ場所時間」を切る
            if (freezeEndTime > 0f && Time.time >= freezeEndTime)
            {
                freezeEndTime = -1f;
                if (TryForceStepToResetStall(out Vector3 stepAfterFreeze))
                {
                    MoveToward(myPos, stepAfterFreeze, 1f);
                    return;
                }
            }

            // 0) いま危険セルなら最優先で退避（硬直中に巻き込まれるのが一番負け筋）
            if (IsDanger(myCell))
            {
                if (TryEvadeToSafe(myPos, out Vector3 evadeNext))
                {
                    MoveToward(myPos, evadeNext, 1f);
                    return;
                }
                // 退避できないなら、最低限「同じ場所」を避ける
                if (TryForceStepToResetStall(out Vector3 step))
                {
                    MoveToward(myPos, step, 1f);
                    return;
                }
                SetMoveSpeedRatio(0f);
                return;
            }

            // 1) 遠距離投げ（接近しない）
            if (TryThrowFromDistance(myPos))
                return;

            // 2) 目的地選択
            Vector3 target = PickTarget(myPos);

            // 3) 次の1手（dangerセルは避ける）
            if (TryGetSafeNextStep(myPos, target, out Vector3 next))
            {
                MoveToward(myPos, next, 1f);
                return;
            }

            // 4) フォールバック（NavMesh）
            if (navi != null)
            {
                navi.SetDestination(target);
                if (navi.IsReady())
                {
                    Vector3 dir = navi.MoveDirection();
                    if (dir.sqrMagnitude > 0.0001f)
                    {
                        MoveDirectionSafe(dir.normalized, 1f);
                        return;
                    }
                }
            }

            // 5) 最後：直線
            Vector3 d = (target - myPos);
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f)
                MoveDirectionSafe(d.normalized, 0.8f);
            else
                SetMoveSpeedRatio(0f);
        }

        // -------------------------
        // Danger map (爆風セル予測)
        // -------------------------
        private void RebuildDangerMap()
        {
            for (int x = 0; x < GridSize; x++)
                for (int z = 0; z < GridSize; z++)
                    danger[x, z] = false;

            if (team == null) return;

            int hitCount = Physics.OverlapSphereNonAlloc(GetPosition(), bombDetectRadius, bombHits);
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = bombHits[i];
                if (col == null) continue;

                Bomb bomb = col.GetComponentInParent<Bomb>();
                if (bomb == null) continue;

                // NOTE: 変数名が違うならここだけ直す
                int level = bomb.level;

                Vector3Int bcell = team.WorldToCell(bomb.transform.position);
                MarkExplosionCross(bcell, level);
            }
        }

        private void MarkExplosionCross(Vector3Int center, int level)
        {
            if (!InGrid(center)) return;

            danger[center.x, center.z] = true;

            MarkDir(center, level, 1, 0);
            MarkDir(center, level, -1, 0);
            MarkDir(center, level, 0, 1);
            MarkDir(center, level, 0, -1);
        }

        private void MarkDir(Vector3Int c, int level, int dx, int dz)
        {
            for (int s = 1; s <= level; s++)
            {
                Vector3Int n = new Vector3Int(c.x + dx * s, 0, c.z + dz * s);
                if (!InGrid(n)) break;

                // 壁で止まる（壁セル自体は爆風が通らない前提）
                if (!team.IsWalkable(n))
                    break;

                danger[n.x, n.z] = true;
            }
        }

        private bool InGrid(Vector3Int cell)
        {
            return cell.x >= Group05Team.GridMin && cell.x <= Group05Team.GridMax
                && cell.z >= Group05Team.GridMin && cell.z <= Group05Team.GridMax;
        }

        private bool IsDanger(Vector3Int cell)
        {
            if (!InGrid(cell)) return true;
            return danger[cell.x, cell.z];
        }

        // -------------------------
        // Anti-stall: 強制1歩
        // -------------------------
        private bool TryForceStepToResetStall(out Vector3 stepWorld)
        {
            stepWorld = GetPosition();
            if (team == null) return false;

            Vector3Int c = team.WorldToCell(GetPosition());

            // 4近傍から「歩けて」「dangerじゃない」を優先
            Vector3Int[] cand =
            {
                new Vector3Int(c.x + 1, 0, c.z),
                new Vector3Int(c.x - 1, 0, c.z),
                new Vector3Int(c.x, 0, c.z + 1),
                new Vector3Int(c.x, 0, c.z - 1),
            };

            for (int i = 0; i < cand.Length; i++)
            {
                Vector3Int n = cand[i];
                if (!InGrid(n)) continue;
                if (!team.IsWalkable(n)) continue;
                if (IsDanger(n)) continue;

                stepWorld = team.CellToWorld(n);
                return true;
            }

            // 全部dangerなら「walkable」だけでもいい（停止よりマシ）
            for (int i = 0; i < cand.Length; i++)
            {
                Vector3Int n = cand[i];
                if (!InGrid(n)) continue;
                if (!team.IsWalkable(n)) continue;

                stepWorld = team.CellToWorld(n);
                return true;
            }

            return false;
        }

        // -------------------------
        // Evade (危険セルからの退避)
        // -------------------------
        private bool TryEvadeToSafe(Vector3 myPos, out Vector3 evadeNext)
        {
            evadeNext = myPos;

            Vector3Int start = team.WorldToCell(myPos);
            if (!InGrid(start)) return false;

            // 深さ制限BFS（ノード数小さいので配列でOK）
            const int MaxQ = 512;
            Vector3Int[] qCell = new Vector3Int[MaxQ];
            int[] qDepth = new int[MaxQ];
            int head = 0, tail = 0;

            bool[,] visited = new bool[GridSize, GridSize];
            visited[start.x, start.z] = true;
            qCell[tail] = start;
            qDepth[tail] = 0;
            tail++;

            Vector3Int best = start;
            bool found = false;

            while (head < tail)
            {
                Vector3Int c = qCell[head];
                int d = qDepth[head];
                head++;

                if (!IsDanger(c) && team.IsWalkable(c))
                {
                    best = c;
                    found = true;
                    break;
                }

                if (d >= evadeSearchDepth) continue;

                TryEnqueue(c.x + 1, c.z, d + 1);
                TryEnqueue(c.x - 1, c.z, d + 1);
                TryEnqueue(c.x, c.z + 1, d + 1);
                TryEnqueue(c.x, c.z - 1, d + 1);
            }

            if (!found) return false;

            Vector3 goal = team.CellToWorld(best);
            if (team.TryGetNextStepOnGrid(myPos, goal, out Vector3 next))
            {
                evadeNext = next;
                return true;
            }

            return false;

            void TryEnqueue(int x, int z, int depth)
            {
                if (x < Group05Team.GridMin || x > Group05Team.GridMax) return;
                if (z < Group05Team.GridMin || z > Group05Team.GridMax) return;
                if (visited[x, z]) return;

                Vector3Int n = new Vector3Int(x, 0, z);
                if (!team.IsWalkable(n)) return;

                visited[x, z] = true;
                if (tail < MaxQ)
                {
                    qCell[tail] = n;
                    qDepth[tail] = depth;
                    tail++;
                }
            }
        }

        // -------------------------
        // Throw (遠距離先制 + 停止ペナルティ対策)
        // -------------------------
        private bool TryThrowFromDistance(Vector3 myPos)
        {
            // 直近で同じセルに居すぎるなら、投げると3.5秒固定で死にやすいので投げない
            float sameCellTime = Time.time - lastCellChangeTime;
            float latestSafeThrowTime = stallLimitSeconds - throwFreezeSeconds - stallSafetyMargin; // 10 - 3.5 - 0.5 = 6.0
            if (sameCellTime > latestSafeThrowTime)
                return false;

            // 近くが危険なら投げない（硬直死）
            if (HasNearbyDanger(myPos, 2.5f))
                return false;

            // 敵2体のうち生存してる方を狙う
            for (int e = 0; e < 2; e++)
            {
                if (!team.TryGetEnemy(e, out Vector3 epos, out bool alive) || !alive)
                    continue;

                Vector3 delta = epos - myPos;
                delta.y = 0f;

                float dist = delta.magnitude;
                if (dist < 0.001f) continue;
                if (dist > maxThrowDistance) continue;

                // 射線（縦横）だけを狙う：先制で強い
                if (!GetAlignedCardinal(delta, out Vector3 dir))
                    continue;

                // 自爆ライン回避（安全距離）
                float minSafe = 4f + throwSafetyMargin;
                if (dist < minSafe) continue;

                // 味方が射線にいるなら投げない
                if (IsFriendOnLine(myPos, dir, dist))
                    continue;

                // 壁が手前にあるなら投げない（手前で止まって自爆しやすい）
                float wall = DistanceToWall(dir);
                if (wall + 0.1f < dist)
                    continue;

                // 自分のセルがdangerなら投げない（硬直死）
                if (IsDanger(team.WorldToCell(myPos)))
                    continue;

                // 投げる
                SetDirection(dir);
                SetMoveSpeedRatio(0f);
                ShootBomb();

                // フリーズ明けに1歩動かすためのトリガ
                freezeEndTime = Time.time + throwFreezeSeconds;

                return true;
            }

            return false;
        }

        private bool HasNearbyDanger(Vector3 pos, float radius)
        {
            Vector3Int c = team.WorldToCell(pos);
            if (!InGrid(c)) return true;

            int r = Mathf.Clamp(Mathf.CeilToInt(radius), 1, 3);
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    Vector3Int n = new Vector3Int(c.x + dx, 0, c.z + dz);
                    if (!InGrid(n)) continue;
                    if (IsDanger(n)) return true;
                }
            }
            return false;
        }

        private bool GetAlignedCardinal(Vector3 delta, out Vector3 dir)
        {
            dir = Vector3.zero;
            float ax = Mathf.Abs(delta.x);
            float az = Mathf.Abs(delta.z);

            // ほぼ縦 or 横
            if (ax > az * 2f)
            {
                dir = (delta.x > 0f) ? Vector3.right : Vector3.left;
                return true;
            }
            if (az > ax * 2f)
            {
                dir = (delta.z > 0f) ? Vector3.forward : Vector3.back;
                return true;
            }

            return false;
        }

        private bool IsFriendOnLine(Vector3 myPos, Vector3 dir, float enemyDist)
        {
            GameObject friend = GetFriend();
            if (friend == null) return false;

            Vector3 f = friend.transform.position;
            Vector3 df = f - myPos;
            df.y = 0f;

            float proj = Vector3.Dot(df, dir);
            if (proj <= 0f || proj >= enemyDist) return false;

            Vector3 perp = df - dir * proj;
            return perp.sqrMagnitude < 0.25f; // 0.5m以内
        }

        // -------------------------
        // Move (danger回避込み)
        // -------------------------
        private bool TryGetSafeNextStep(Vector3 myPos, Vector3 target, out Vector3 next)
        {
            next = myPos;

            if (!team.TryGetNextStepOnGrid(myPos, target, out Vector3 n))
                return false;

            Vector3Int c = team.WorldToCell(n);
            if (!IsDanger(c))
            {
                next = n;
                return true;
            }

            // 次セルが危険なら、近傍の安全セルへ
            Vector3Int myCell = team.WorldToCell(myPos);

            Vector3Int[] cand =
            {
                new Vector3Int(myCell.x + 1, 0, myCell.z),
                new Vector3Int(myCell.x - 1, 0, myCell.z),
                new Vector3Int(myCell.x, 0, myCell.z + 1),
                new Vector3Int(myCell.x, 0, myCell.z - 1),
            };

            float best = float.PositiveInfinity;
            Vector3Int bestCell = myCell;
            bool found = false;

            for (int i = 0; i < cand.Length; i++)
            {
                Vector3Int cc = cand[i];
                if (!InGrid(cc)) continue;
                if (!team.IsWalkable(cc)) continue;
                if (IsDanger(cc)) continue;

                Vector3 w = team.CellToWorld(cc);
                float d = (w - target).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    bestCell = cc;
                    found = true;
                }
            }

            if (!found)
            {
                // 安全セルがないなら停止より「強制1歩」（anti-stall）
                if (TryForceStepToResetStall(out Vector3 step))
                {
                    next = step;
                    return true;
                }
                return false;
            }

            next = team.CellToWorld(bestCell);
            return true;
        }

        private void MoveToward(Vector3 from, Vector3 to, float speed)
        {
            Vector3 dir = (to - from);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
            {
                SetMoveSpeedRatio(0f);
                return;
            }
            MoveDirectionSafe(dir.normalized, speed);
        }

        private void MoveDirectionSafe(Vector3 dir, float speed)
        {
            // 壁に突っ込む無駄を抑える
            float wall = DistanceToWall(dir);
            if (wall < 0.75f)
            {
                Vector3 left = new Vector3(-dir.z, 0f, dir.x);
                Vector3 right = new Vector3(dir.z, 0f, -dir.x);
                dir = (DistanceToWall(left) > DistanceToWall(right)) ? left : right;
            }

            // 次セルが危険なら止める…ではなく「別方向」か「強制1歩」へ逃がす
            Vector3Int myCell = team.WorldToCell(GetPosition());
            Vector3Int nextCell = myCell + new Vector3Int(Mathf.RoundToInt(dir.x), 0, Mathf.RoundToInt(dir.z));
            if (InGrid(nextCell) && IsDanger(nextCell))
            {
                if (TryForceStepToResetStall(out Vector3 step))
                {
                    Vector3 d = (step - GetPosition());
                    d.y = 0f;
                    if (d.sqrMagnitude > 0.0001f)
                    {
                        SetDirection(d.normalized);
                        SetMoveSpeedRatio(Mathf.Clamp01(speed));
                        return;
                    }
                }
                SetMoveSpeedRatio(0f);
                return;
            }

            SetDirection(dir);
            SetMoveSpeedRatio(Mathf.Clamp01(speed));
        }

        // -------------------------
        // Target selection
        // -------------------------
        private Vector3 PickTarget(Vector3 myPos)
        {
            // 敵優先：近い方
            Vector3 bestEnemy = myPos;
            float best = float.PositiveInfinity;
            bool any = false;

            for (int i = 0; i < 2; i++)
            {
                if (team.TryGetEnemy(i, out Vector3 epos, out bool alive) && alive)
                {
                    float d = (epos - myPos).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        bestEnemy = epos;
                        any = true;
                    }
                }
            }

            if (any)
            {
                if (selfRole == 0)
                {
                    // 攻め：敵近辺の「安全」セルへ
                    return FindSafeNear(bestEnemy, 2);
                }
                else
                {
                    // 回り込み：敵の横
                    GameObject friend = GetFriend();
                    Vector3 fpos = friend ? friend.transform.position : myPos;
                    Vector3 v = (bestEnemy - fpos);
                    v.y = 0f;

                    if (v.sqrMagnitude < 0.01f) return bestEnemy;

                    v.Normalize();
                    Vector3 side = new Vector3(-v.z, 0f, v.x);

                    Vector3 c1 = bestEnemy + side * 3f;
                    Vector3 c2 = bestEnemy - side * 3f;

                    Vector3 s1 = FindSafeNear(c1, 2);
                    Vector3 s2 = FindSafeNear(c2, 2);

                    float d1 = (s1 - myPos).sqrMagnitude;
                    float d2 = (s2 - myPos).sqrMagnitude;
                    return (d1 < d2) ? s1 : s2;
                }
            }

            // 敵がいない：アイテムへ（必要時のみ）
            if (GameManager.instance != null)
            {
                int n = GameManager.instance.NumItems();
                Vector3 bestItem = myPos;
                float bestD = float.PositiveInfinity;

                for (int i = 0; i < n; i++)
                {
                    if (!GameManager.instance.IsItemAvailable(i))
                        continue;

                    Vector3 ipos = GameManager.instance.ItemPosition(i);
                    float d = (ipos - myPos).sqrMagnitude;
                    if (d < bestD)
                    {
                        bestD = d;
                        bestItem = ipos;
                    }
                }

                return FindSafeNear(bestItem, 2);
            }

            return myPos;
        }

        private Vector3 FindSafeNear(Vector3 world, int radiusCells)
        {
            Vector3Int c = team.WorldToCell(world);
            if (!InGrid(c)) return world;

            if (!IsDanger(c) && team.IsWalkable(c))
                return team.CellToWorld(c);

            Vector3Int best = c;
            float bestScore = float.NegativeInfinity;

            for (int dx = -radiusCells; dx <= radiusCells; dx++)
            {
                for (int dz = -radiusCells; dz <= radiusCells; dz++)
                {
                    Vector3Int n = new Vector3Int(c.x + dx, 0, c.z + dz);
                    if (!InGrid(n)) continue;
                    if (!team.IsWalkable(n)) continue;
                    if (IsDanger(n)) continue;

                    float score = -Mathf.Abs(dx) - Mathf.Abs(dz);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = n;
                    }
                }
            }

            return team.CellToWorld(best);
        }
    }
}
