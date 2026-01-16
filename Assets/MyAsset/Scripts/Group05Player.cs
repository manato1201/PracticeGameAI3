using UnityEngine;

namespace Group05
{
    public class Group05Player : Pawn
    {
        private const int GROUP_NO = 5;

        private NavMeshTool navi;
        private Group05Team team;
        private int myIndex = -1;

        private Vector3 currentGoal;
        private float goalReplanTimer = 0f;

        // ---- shooting / retreat ----
        private float shootCooldown = 0f;
        private float noShotTimer = 0f;

        private enum State { Chase, Retreat }
        private State state = State.Chase;
        private float retreatTimer = 0f;

        [Header("Replan")]
        [SerializeField] private float replanInterval = 0.25f;

        [Header("Avoid teammate congestion")]
        [SerializeField] private float mateKeepDist = 2.2f;
        [SerializeField] private float mateHardAvoid = 1.2f;

        [Header("Shoot rules")]
        [SerializeField] private float alignEps = 0.55f;   // 縦横揃い判定
        [SerializeField] private float minShootDist = 2.0f; // 自爆防止（近すぎは撃たない）
        [SerializeField] private float allyNear = 3.0f;     // 味方が近いと基本撃たない
        [SerializeField] private float crossRange = 4.0f;   // 十字巻き込み判定
        [SerializeField] private float allyEnemyClose = 1.8f; // 味方と敵が近いなら攻撃寄せ

        [Header("Retreat")]
        [SerializeField] private float giveUpTime = 1.25f;   // 狙って撃てない時間
        [SerializeField] private float retreatTime = 0.60f;  // 撤退継続
        [SerializeField] private float retreatDist = 3.0f;   // 撤退距離

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

            if (shootCooldown > 0f) shootCooldown -= Time.deltaTime;
            sideLockTimer -= Time.deltaTime;

            Vector3 myPos = GetPosition();
            Vector3 matePos = (team != null) ? team.GetMatePosition(myIndex) : Vector3.zero;

            int enemyAlive = (team != null) ? team.EnemyAliveCount() : 2;
            Vector3 enemyPos = (team != null) ? team.GetEnemyPositionByAssigned(myIndex, myPos) : Vector3.zero;

            // ---- State machine ----
            if (state == State.Retreat)
            {
                retreatTimer -= Time.deltaTime;
                DoRetreat(myPos, matePos, enemyPos);
                if (retreatTimer <= 0f)
                {
                    state = State.Chase;
                    noShotTimer = 0f;
                }
                return;
            }

            // 近距離は NavMesh の追従で回転しやすいので「整列＋撃つ」を優先
            if (enemyPos != Vector3.zero)
            {
                if (TryShoot(enemyPos, myPos, matePos, enemyAlive))
                {
                    // 撃てた
                    noShotTimer = 0f;
                }
                else
                {
                    // 撃てない時間が続くなら撤退して作り直す
                    noShotTimer += Time.deltaTime;
                    if (noShotTimer >= giveUpTime)
                    {
                        state = State.Retreat;
                        retreatTimer = retreatTime;
                        return;
                    }
                }
            }

            // 追跡ゴール更新（頻繁に変えると角で震える）
            goalReplanTimer -= Time.deltaTime;
            if (goalReplanTimer <= 0f)
            {
                goalReplanTimer = replanInterval;

                if (team != null)
                    currentGoal = team.GetAttackGoal(myIndex, myPos);
                else
                    currentGoal = new Vector3(8f, 0f, 8f);

                // 味方が近いならゴールを少し左右にズラす（詰まり軽減）
                currentGoal = ApplyGoalOffsetByMate(currentGoal, myPos, matePos);

                navi.SetDestination(currentGoal);
            }

            Vector3 dir = navi.IsReady() ? navi.MoveDirection() : GetDirection();
            dir = ApplySeparation(dir, myPos, matePos);

            SetDirection(dir);
            SetMoveSpeedRatio(1.0f);
        }

        // ----------------------------
        // 撤退：敵から距離を取って位置を作り直す（直線後退＋横成分）
        // ----------------------------
        private void DoRetreat(Vector3 myPos, Vector3 matePos, Vector3 enemyPos)
        {
            Vector3 away = Vector3.zero;
            if (enemyPos != Vector3.zero)
            {
                away = myPos - enemyPos; away.y = 0f;
            }
            if (away.sqrMagnitude < 0.0001f) away = Vector3.back;
            away.Normalize();

            Vector3 side = GetLockedSide(myPos, enemyPos);
            Vector3 dir = (away + side * 0.7f).normalized;

            // 味方が近すぎるなら、さらに離す
            if (matePos != Vector3.zero)
            {
                Vector3 dm = myPos - matePos; dm.y = 0f;
                if (dm.sqrMagnitude < mateHardAvoid * mateHardAvoid && dm.sqrMagnitude > 0.0001f)
                {
                    dir = (dir + dm.normalized * 1.2f).normalized;
                }
            }

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
        // 右か左で迷ったら右。左優先個体なら左。短時間ロックでクルクル防止
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
        // 射撃：
        // - 自爆しない（近すぎは撃たない）
        // - 味方巻き込み抑制（ただし「敵が1体」なら緩める）
        // - 味方と敵が近いなら攻撃優先に寄せる
        // ----------------------------
        private bool TryShoot(Vector3 enemyPos, Vector3 myPos, Vector3 matePos, int enemyAlive)
        {
            if (shootCooldown > 0f) return false;

            Vector3 d = enemyPos - myPos; d.y = 0f;
            if (d.sqrMagnitude < minShootDist * minShootDist) return false;

            // 敵が残り1体ならより攻撃的
            bool lastEnemy = (enemyAlive <= 1);

            // 味方と敵が近いなら攻撃寄せ
            bool allyEnemyNear = false;
            if (matePos != Vector3.zero && enemyPos != Vector3.zero)
                allyEnemyNear = (matePos - enemyPos).sqrMagnitude <= (allyEnemyClose * allyEnemyClose);

            Vector3 m = Vector3.zero;
            if (matePos != Vector3.zero)
            {
                m = matePos - myPos; m.y = 0f;

                // 味方が近いなら撃たない（ただし allyEnemyNear / lastEnemy は許容）
                if (!allyEnemyNear && !lastEnemy && m.sqrMagnitude < allyNear * allyNear) return false;

                // 十字爆風で巻き込みやすい配置なら撃たない（ただし allyEnemyNear / lastEnemy は許容）
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
    }
}
