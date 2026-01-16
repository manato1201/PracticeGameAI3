using UnityEngine;

namespace Group05
{
    public class Group05Player : Pawn
    {
        private const int GROUP_NO = 5;

        private static readonly Vector3[] DIRS =
        {
            Vector3.forward,
            Vector3.back,
            Vector3.left,
            Vector3.right,
        };

        private NavMeshTool navi;
        private Group05Team team;
        private int myIndex = -1;

        private Vector3 currentGoal;
        private float goalReplanTimer = 0f;

        // ---- state ----
        private enum State { Chase, Retreat, BombEvade }
        private State state = State.Chase;

        // ---- timers ----
        private float shootCooldown = 0f;
        private float noShotTimer = 0f;
        private float retreatTimer = 0f;
        private float bombEvadeTimer = 0f;
        private float zigzagFlip = 0f;
        private int zigzagSign = 1;

        [Header("Replan")]
        [SerializeField] private float replanInterval = 0.25f;

        [Header("Avoid teammate congestion")]
        [SerializeField] private float mateKeepDist = 2.2f;
        [SerializeField] private float mateHardAvoid = 1.2f;

        [Header("Shoot rules")]
        [SerializeField] private float alignEps = 0.55f;       // 縦横揃い判定
        [SerializeField] private float minShootDist = 2.0f;    // 自爆防止（近すぎは撃たない）
        [SerializeField] private float allyNear = 3.0f;        // 味方が近いと基本撃たない
        [SerializeField] private float crossRange = 4.0f;      // 十字巻き込み判定
        [SerializeField] private float allyEnemyClose = 1.8f;  // 味方と敵が近いなら攻撃寄せ

        [Header("Retreat")]
        [SerializeField] private float giveUpTime = 1.25f;     // 狙って撃てない時間
        [SerializeField] private float retreatTime = 0.60f;    // 撤退継続
        [SerializeField] private float retreatAfterShot = 0.35f; // 撃った直後の自爆回避撤退

        [Header("Bomb Avoid")]
        [SerializeField] private float bombDangerDist = 3.4f;  // 十字爆風想定（最低限）
        [SerializeField] private float bombEvadeTime = 0.55f;  // 回避継続
        [SerializeField] private float zigzagFlipInterval = 0.12f; // ジグザグ切替
        [SerializeField] private float zigzagWeave = 0.55f;    // ジグザグの蛇行量

        // 右優先ロック（迷ったら右、ただし個体差で左右を変える）
        private bool preferLeft = false;
        private float sideLockTimer = 0f;
        private Vector3 lockedSide = Vector3.right;

        void Start()
        {
            SetGroupNo(GROUP_NO);

            navi = GetNavMeshTool();
            team = GetComponentInParent<Group05Team>();
            if (team != null)
            {
                team.Register(this);
                myIndex = team.IndexOf(this);
            }

            // 2体が同じラインに寄りにくいように “右/左” を個体で分ける
            preferLeft = (myIndex == 1);

            currentGoal = GetPosition();
        }

        void Update()
        {
            if (team != null && myIndex < 0)
            {
                myIndex = team.IndexOf(this);
                preferLeft = (myIndex == 1);
            }

            float dt = Time.deltaTime;
            if (shootCooldown > 0f) shootCooldown -= dt;
            sideLockTimer -= dt;

            Vector3 myPos = GetPosition();
            Vector3 matePos = (team != null) ? team.GetMatePosition(myIndex) : Vector3.zero;

            int enemyAlive = (team != null) ? team.EnemyAliveCount() : 2;
            Vector3 enemyPos = (team != null) ? team.GetEnemyPositionByAssigned(myIndex, myPos) : Vector3.zero;

            // ====== 0) 爆弾回避は最優先（自爆も他爆も含む） ======
            if (DetectBombAxis(myPos, out Vector3 dangerAxis))
            {
                state = State.BombEvade;
                bombEvadeTimer = bombEvadeTime;
                zigzagFlip = 0f;
                zigzagSign = 1;
            }

            if (state == State.BombEvade)
            {
                bombEvadeTimer -= dt;
                DoBombEvade(myPos, matePos, dangerAxis: GuessDangerAxis(myPos));
                if (bombEvadeTimer <= 0f)
                {
                    state = State.Chase;
                    noShotTimer = 0f;
                }
                return;
            }

            // ====== 1) 撤退中も爆弾優先（上で処理済み） ======
            if (state == State.Retreat)
            {
                retreatTimer -= dt;
                DoRetreat(myPos, matePos, enemyPos);
                if (retreatTimer <= 0f)
                {
                    state = State.Chase;
                    noShotTimer = 0f;
                }
                return;
            }

            // ====== 2) 近距離は「整列＋撃つ」を優先（NavMesh周回を抑える） ======
            if (enemyPos != Vector3.zero)
            {
                bool shot = TryShoot(enemyPos, myPos, matePos, enemyAlive);
                if (shot)
                {
                    // 撃てたら自爆回避のため即撤退（短時間）
                    state = State.Retreat;
                    retreatTimer = retreatAfterShot;
                    noShotTimer = 0f;
                    return;
                }
                else
                {
                    noShotTimer += dt;
                    if (noShotTimer >= giveUpTime)
                    {
                        state = State.Retreat;
                        retreatTimer = retreatTime;
                        return;
                    }
                }
            }

            // ====== 3) 追跡ゴール更新（頻繁に変えると角で震える） ======
            goalReplanTimer -= dt;
            if (goalReplanTimer <= 0f)
            {
                goalReplanTimer = replanInterval;

                currentGoal = (team != null) ? team.GetAttackGoal(myIndex, myPos) : new Vector3(8f, 0f, 8f);

                // 味方が近いならゴールを左右にズラして詰まり軽減
                currentGoal = ApplyGoalOffsetByMate(currentGoal, myPos, matePos);

                navi.SetDestination(currentGoal);
            }

            Vector3 dir = navi.IsReady() ? navi.MoveDirection() : GetDirection();
            dir = ApplySeparation(dir, myPos, matePos);

            SetDirection(dir);
            SetMoveSpeedRatio(1.0f);
        }

        // ----------------------------
        // 爆弾検知：4方向Rayで、危険距離内に Bomb が「先に」当たったら危険
        // ----------------------------
        private bool DetectBombAxis(Vector3 myPos, out Vector3 dangerAxis)
        {
            dangerAxis = Vector3.zero;

            Vector3 start = myPos;
            start.y = 0.5f;

            for (int i = 0; i < 4; i++)
            {
                Ray ray = new Ray(start, DIRS[i]);
                if (!Physics.Raycast(ray, out var hit, bombDangerDist)) continue;
                if (hit.collider == null) continue;

                // 壁が先なら爆風は遮られる前提で無視
                if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Wall"))
                    continue;

                if (hit.collider.CompareTag("Bomb"))
                {
                    // forward/backに爆弾 → 縦軸が危険（横へ抜ける）
                    // left/rightに爆弾 → 横軸が危険（縦へ抜ける）
                    dangerAxis = (i == 0 || i == 1) ? Vector3.forward : Vector3.right;
                    return true;
                }
            }
            return false;
        }

        // BombEvade中に「今どっち軸が危険か」を雑に復元（毎フレームDetectを重ねすぎない）
        private Vector3 GuessDangerAxis(Vector3 myPos)
        {
            if (DetectBombAxis(myPos, out var axis)) return axis;
            return Vector3.forward; // 見失ったらどちらでも良いが、回避自体は継続
        }

        // ----------------------------
        // 爆弾回避：直交方向に抜けつつジグザグ（直線逃げ禁止）
        // ----------------------------
        private void DoBombEvade(Vector3 myPos, Vector3 matePos, Vector3 dangerAxis)
        {
            Vector3 baseEvade = (dangerAxis == Vector3.forward) ? Vector3.right : Vector3.forward;

            // 左右のどっちが空いてるか（僅差なら右、ただし preferLeft は左）
            float d0 = DistanceToWallDir(myPos, baseEvade);
            float d1 = DistanceToWallDir(myPos, -baseEvade);

            Vector3 bestBase;
            float diff = d0 - d1;
            const float tieEps = 0.05f;

            if (Mathf.Abs(diff) <= tieEps)
            {
                bestBase = preferLeft ? -baseEvade : baseEvade; // 迷ったら右（個体は左）
            }
            else
            {
                bestBase = (d0 >= d1) ? baseEvade : -baseEvade;
            }

            // ジグザグの切替
            zigzagFlip -= Time.deltaTime;
            if (zigzagFlip <= 0f)
            {
                zigzagFlip = zigzagFlipInterval;
                zigzagSign = -zigzagSign;
            }

            Vector3 weave = (dangerAxis == Vector3.forward) ? Vector3.forward : Vector3.right;
            Vector3 dir = (bestBase + weave * (zigzagWeave * zigzagSign)).normalized;

            // 味方が近すぎるなら離す（巻き込み＆詰まり防止）
            if (matePos != Vector3.zero)
            {
                Vector3 dm = myPos - matePos; dm.y = 0f;
                if (dm.sqrMagnitude < mateHardAvoid * mateHardAvoid && dm.sqrMagnitude > 0.0001f)
                    dir = (dir + dm.normalized * 1.2f).normalized;
            }

            // 壁に突っ込むならベース方向のみ
            if (DistanceToWallDir(myPos, dir) < 0.8f)
                dir = bestBase.normalized;

            SetDirection(dir);
            SetMoveSpeedRatio(1.0f);
        }

        // ----------------------------
        // 撤退：敵から距離を取って位置を作り直す（爆風ラインから抜ける）
        // ----------------------------
        private void DoRetreat(Vector3 myPos, Vector3 matePos, Vector3 enemyPos)
        {
            Vector3 away = Vector3.back;
            if (enemyPos != Vector3.zero)
            {
                away = myPos - enemyPos; away.y = 0f;
                if (away.sqrMagnitude < 0.0001f) away = Vector3.back;
            }
            away.Normalize();

            Vector3 side = GetLockedSide(myPos, myPos + away);
            Vector3 dir = (away + side * 0.7f).normalized;

            // 味方が近すぎるならさらに離す
            if (matePos != Vector3.zero)
            {
                Vector3 dm = myPos - matePos; dm.y = 0f;
                if (dm.sqrMagnitude < mateHardAvoid * mateHardAvoid && dm.sqrMagnitude > 0.0001f)
                    dir = (dir + dm.normalized * 1.2f).normalized;
            }

            // 壁に近いなら反対側へ（角震えの抑制）
            if (DistanceToWallDir(myPos, dir) < 0.8f)
                dir = (dir + side).normalized;

            SetDirection(dir);
            SetMoveSpeedRatio(1.0f);
        }

        // ----------------------------
        // 味方に寄らないようにゴールを左右へオフセット
        // ----------------------------
        private Vector3 ApplyGoalOffsetByMate(Vector3 goal, Vector3 myPos, Vector3 matePos)
        {
            if (matePos == Vector3.zero) return goal;

            Vector3 dm = myPos - matePos; dm.y = 0f;
            if (dm.sqrMagnitude > mateKeepDist * mateKeepDist) return goal;

            Vector3 side = GetLockedSide(myPos, goal);
            return goal + side * 1.2f;
        }

        private Vector3 ApplySeparation(Vector3 dir, Vector3 myPos, Vector3 matePos)
        {
            if (matePos == Vector3.zero) return dir;

            Vector3 dm = myPos - matePos; dm.y = 0f;
            float dsq = dm.sqrMagnitude;

            if (dsq > 0.0001f && dsq < mateKeepDist * mateKeepDist)
            {
                Vector3 sep = dm.normalized;
                Vector3 side = GetLockedSide(myPos, myPos + dir);
                dir = (dir + sep * 1.0f + side * 0.6f).normalized;
            }
            return dir;
        }

        // ----------------------------
        // 右か左で迷ったら右（個体差で左）。短時間ロックでクルクル防止
        // ----------------------------
        private Vector3 GetLockedSide(Vector3 from, Vector3 to)
        {
            Vector3 v = to - from; v.y = 0f;
            if (v.sqrMagnitude < 0.0001f) v = Vector3.forward;
            v.Normalize();

            Vector3 right = new Vector3(v.z, 0f, -v.x);
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            right.Normalize();

            if (sideLockTimer <= 0f)
            {
                lockedSide = preferLeft ? -right : right;
                sideLockTimer = 0.30f;
            }
            return lockedSide;
        }

        // ----------------------------
        // 壁距離（Wallレイヤー優先。これがブレるとガタガタの原因）
        // ----------------------------
        private float DistanceToWallDir(Vector3 myPos, Vector3 dir)
        {
            Vector3 start = myPos;
            start.y = 0.5f;

            if (dir.sqrMagnitude < 0.0001f) return 10f;
            dir.Normalize();

            Ray ray = new Ray(start, dir);
            if (Physics.Raycast(ray, out var hit, 10f))
            {
                if (hit.collider != null && hit.collider.gameObject.layer == LayerMask.NameToLayer("Wall"))
                    return hit.distance;

                // 壁じゃないものに当たった場合は「壁ではない」ので遠い扱い（震え軽減）
                return 10f;
            }
            return 10f;
        }

        // ----------------------------
        // 射撃：
        // - 自爆しない（近すぎは撃たない）
        // - 味方巻き込み抑制（ただし「敵が1体」なら緩める）
        // - 味方と敵が近いなら攻撃優先に寄せる
        // ----------------------------
        private bool TryShoot(Vector3 enemyPos, Vector3 myPos, Vector3 matePos, int enemyAlive)
        {
            if (shootCooldown > 0f) return false;
            // --- 追加：壁越し撃ち禁止（壁に当てて自滅するのを防ぐ） ---
            if (IsShotBlockedByWall(myPos, enemyPos))
                return false;

            // --- 追加：撃つ向きの手前に壁が近いなら撃たない（壁密着自爆防止） ---
            if (IsWallTooCloseInShotDirection(myPos, enemyPos))
                return false;

            Vector3 d = enemyPos - myPos; d.y = 0f;
            if (d.sqrMagnitude < minShootDist * minShootDist) return false;

            bool lastEnemy = (enemyAlive <= 1);

            // 味方と敵が近いなら攻撃寄せ
            bool allyEnemyNear = false;
            if (matePos != Vector3.zero && enemyPos != Vector3.zero)
                allyEnemyNear = (matePos - enemyPos).sqrMagnitude <= (allyEnemyClose * allyEnemyClose);

            if (matePos != Vector3.zero)
            {
                Vector3 m = matePos - myPos; m.y = 0f;

                if (!allyEnemyNear && !lastEnemy && m.sqrMagnitude < allyNear * allyNear) return false;

                if (!allyEnemyNear && !lastEnemy)
                {
                    if (Mathf.Abs(m.x) < alignEps && Mathf.Abs(m.z) < crossRange) return false;
                    if (Mathf.Abs(m.z) < alignEps && Mathf.Abs(m.x) < crossRange) return false;
                }
            }

            // 縦横が揃ったら撃つ
            if (Mathf.Abs(d.x) < alignEps)
            {
                SetDirection((d.z >= 0f) ? Vector3.forward : Vector3.back);
                ShootBomb();
                shootCooldown = 1.0f;
                return true;
            }
            if (Mathf.Abs(d.z) < alignEps)
            {
                SetDirection((d.x >= 0f) ? Vector3.right : Vector3.left);
                ShootBomb();
                shootCooldown = 1.0f;
                return true;
            }

            return false;
        }

        // 壁越し（直線が壁で遮られている）なら true
        private bool IsShotBlockedByWall(Vector3 myPos, Vector3 enemyPos)
        {
            Vector3 a = myPos;   a.y = 0.5f;
            Vector3 b = enemyPos; b.y = 0.5f;

            Vector3 dir = b - a;
            float dist = dir.magnitude;
            if (dist <= 0.01f) return false;

            dir /= dist;

            // 途中で Wall に当たったら「壁越し」
            if (Physics.Raycast(a, dir, out var hit, dist))
            {
                if (hit.collider != null && hit.collider.gameObject.layer == LayerMask.NameToLayer("Wall"))
                    return true;
            }
            return false;
        }

// 撃つ予定の方向に「近い壁」があるなら true（壁に向けて撃つ自爆を減らす）
        private bool IsWallTooCloseInShotDirection(Vector3 myPos, Vector3 enemyPos)
        {
            Vector3 d = enemyPos - myPos; d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return true;

            // 十字のどっちで撃つか（TryShootと同じ判定）
            Vector3 shotDir;
            if (Mathf.Abs(d.x) < alignEps)
                shotDir = (d.z >= 0f) ? Vector3.forward : Vector3.back;
            else if (Mathf.Abs(d.z) < alignEps)
                shotDir = (d.x >= 0f) ? Vector3.right : Vector3.left;
            else
                return false; // そもそも撃てない状態

            // すぐ目の前に壁があるなら撃たない
            Vector3 start = myPos; start.y = 0.5f;
            if (Physics.Raycast(start, shotDir, out var hit, 1.2f))
            {
                if (hit.collider != null && hit.collider.gameObject.layer == LayerMask.NameToLayer("Wall"))
                    return true;
            }
            return false;
        }

    }
}
