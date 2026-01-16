using UnityEngine;
using System.Collections.Generic;

namespace Group04
{
    public class Group04Player : Pawn
    {
        int groupNo = 4;

        enum State
        {
            SNIPE_PATROL, // 位置取り
            ATTACK,       // 攻撃
            ESCAPE        // 回避
        }
        State currentState = State.SNIPE_PATROL;

        NavMeshTool navi;
        float thinkTimer = 0f;
        float escapeTimer = 0f;

        Vector3 escapeDir = Vector3.zero;
        Vector3 snipeTargetPos = Vector3.zero;
        GameObject lockedEnemy = null;
        Vector3 lastPos = Vector3.zero; // スタック検知用

        readonly Vector3[] SNIPE_POINTS = new Vector3[] {
            new Vector3(1, 0, 1), new Vector3(1, 0, 15),
            new Vector3(15, 0, 15), new Vector3(15, 0, 1),
            new Vector3(1, 0, 8), new Vector3(15, 0, 8),
            new Vector3(8, 0, 1), new Vector3(8, 0, 15)
        };

        void Start()
        {
            navi = GetNavMeshTool();
            SetGroupNo(groupNo);
            thinkTimer = Random.Range(0f, 0.2f);
            SetNextSnipePoint();
            lastPos = GetPosition();
        }

        void Update()
        {
            thinkTimer -= Time.deltaTime;
            if (thinkTimer < 0)
            {
                Think();
                lastPos = GetPosition();
                // ★改良：反応速度を上げるため、思考間隔を0.05秒に短縮
                thinkTimer = 0.05f;
            }

            if (escapeTimer > 0f)
            {
                escapeTimer -= Time.deltaTime;
                if (currentState != State.ESCAPE) currentState = State.ESCAPE;
            }

            switch (currentState)
            {
                case State.SNIPE_PATROL:
                    DoSnipePatrol();
                    break;
                case State.ATTACK:
                    DoAttack();
                    break;
                case State.ESCAPE:
                    DoEscape();
                    break;
            }
        }

        void Think()
        {
            if (escapeTimer > 0f)
            {
                // スタック検知（逃げてるのに動いてないなら解除）
                if (Vector3.Distance(GetPosition(), lastPos) < 0.01f) escapeTimer = 0f;
                else return;
            }

            // 1. 【防御】爆弾回避（最優先）
            if (CheckDanger(out escapeDir))
            {
                currentState = State.ESCAPE;
                escapeTimer = 1.2f; // 爆弾からはしっかり逃げる
                return;
            }

            // 2. 【カウンター判断】敵との遭遇
            GameObject enemy = IsTargetValid(lockedEnemy) ? lockedEnemy : FindFarEnemy();

            if (enemy != null)
            {
                lockedEnemy = enemy;
                float dist = Vector3.Distance(GetPosition(), enemy.transform.position);
                if (dist < 4.0f && IsEnemyFacingMe(enemy))
                {
                    // 逃げ込める横道があるか確認
                    Vector3 runDir;
                    if (HasSafeSideStep(out runDir))
                    {
                        escapeDir = runDir;
                        currentState = State.ESCAPE;
                        escapeTimer = 0.8f; // 短めに隠れてすぐ反撃
                        return;
                    }
                }

                currentState = State.ATTACK;
                return;
            }
            lockedEnemy = null;

            // 味方衝突回避
            if (currentState == State.SNIPE_PATROL)
            {
                GameObject friend = GetFriend();
                if (friend != null && !friend.GetComponent<Pawn>().IsDead())
                {
                    if (Vector3.Distance(GetPosition(), friend.transform.position) < 2.0f)
                        SetNextSnipePoint();
                }
            }

            // 3. 【移動】
            currentState = State.SNIPE_PATROL;
        }

        // =========================================================
        // 行動実行
        // =========================================================

        void DoEscape()
        {
            float distToWall = DistanceToWall(escapeDir);
            float runDist = Mathf.Min(3.0f, distToWall - 0.8f);

            if (distToWall < 0.5f || runDist < 0.1f)
            {
                escapeTimer = 0f;
                return;
            }

            if (navi.IsReady())
            {
                Vector3 runTarget = GetPosition() + escapeDir * runDist;
                navi.SetDestination(runTarget);
                SetDirection(navi.MoveDirection());
                SetMoveSpeedRatio(1.0f);
            }
            else
            {
                SetDirection(escapeDir);
                SetMoveSpeedRatio(1.0f);
            }
        }

        void DoSnipePatrol()
        {
            if (navi.IsReady())
            {
                if (Vector3.Distance(GetPosition(), snipeTargetPos) < 1.5f) SetNextSnipePoint();
                navi.SetDestination(snipeTargetPos);
                SetDirection(navi.MoveDirection());
                SetMoveSpeedRatio(1.0f);
            }
        }

        void DoAttack()
        {
            if (lockedEnemy == null) { currentState = State.SNIPE_PATROL; return; }
            Vector3 dirToEnemy = lockedEnemy.transform.position - GetPosition();
            SetDirection(dirToEnemy);
            SetMoveSpeedRatio(0f);
            ShootBomb();
        }

        // =========================================================
        // センサー・ツール
        // =========================================================

        // 敵がこちらを向いているか判定（内積を使用）
        bool IsEnemyFacingMe(GameObject enemy)
        {
            Vector3 toMe = (GetPosition() - enemy.transform.position).normalized;
            Vector3 enemyForward = enemy.GetComponent<Pawn>().GetDirection(); // Pawnの向きを取得
            // 内積がプラスなら大体こちらを向いている
            return Vector3.Dot(enemyForward, toMe) > 0.5f;
        }

        // 横に逃げ道があるかチェック
        bool HasSafeSideStep(out Vector3 bestDir)
        {
            bestDir = Vector3.zero;
            // 敵からの直線を避ける方向（左右）
            Vector3 toEnemy = (lockedEnemy.transform.position - GetPosition()).normalized;
            Vector3 right = Vector3.Cross(toEnemy, Vector3.up);
            Vector3 left = -right;

            float dRight = DistanceToWall(right);
            float dLeft = DistanceToWall(left);

            // 1.5m以上空いているなら「隠れ場所あり」とみなす
            if (dRight > 1.5f) { bestDir = right; return true; }
            if (dLeft > 1.5f) { bestDir = left; return true; }

            return false;
        }

        void SetNextSnipePoint()
        {
            GameObject friend = GetFriend();
            Vector3 friendPos = (friend != null) ? friend.transform.position : Vector3.zero;
            List<Vector3> validPoints = new List<Vector3>();
            for (int i = 0; i < SNIPE_POINTS.Length; i++)
            {
                if (friend == null || Vector3.Distance(SNIPE_POINTS[i], friendPos) > 10.0f)
                    validPoints.Add(SNIPE_POINTS[i]);
            }
            if (validPoints.Count > 0) snipeTargetPos = validPoints[Random.Range(0, validPoints.Count)];
            else snipeTargetPos = SNIPE_POINTS[Random.Range(0, SNIPE_POINTS.Length)];
        }

        GameObject FindFarEnemy()
        {
            for (int i = 0; i < cardinalDirectionNum; i++)
            {
                Vector3 dir = cardinalDirections[i];
                GameObject foundObj = null;
                if (CheckPlayer(dir, ref foundObj))
                {
                    Pawn p = foundObj.GetComponent<Pawn>();
                    if (p != null && p.GetTeamID() != GetTeamID()) return foundObj;
                }
            }
            return null;
        }

        bool IsTargetValid(GameObject target)
        {
            if (target == null) return false;
            Pawn p = target.GetComponent<Pawn>();
            if (p == null || p.IsDead()) return false;
            Vector3 vectorToTarget = target.transform.position - GetPosition();
            GameObject hitObj = null;
            if (CheckPlayer(vectorToTarget, ref hitObj)) if (hitObj == target) return true;
            return false;
        }

        bool CheckDanger(out Vector3 safeDirection)
        {
            safeDirection = Vector3.zero;
            float dist = 0f;
            for (int i = 0; i < cardinalDirectionNum; i++)
            {
                Vector3 checkDir = cardinalDirections[i];
                if (CheckBomb(checkDir, ref dist))
                {
                    if (dist < 5.0f)
                    {
                        Vector3 runAwayDir = -checkDir;
                        Vector3 rightDir = Vector3.Cross(runAwayDir, Vector3.up);
                        Vector3 leftDir = -rightDir;
                        float distRight = DistanceToWall(rightDir);
                        float distLeft = DistanceToWall(leftDir);
                        float distBack = DistanceToWall(runAwayDir);

                        if (distRight > distLeft)
                        {
                            if (distRight > 1.0f) safeDirection = rightDir;
                            else safeDirection = runAwayDir;
                        }
                        else
                        {
                            if (distLeft > 1.0f) safeDirection = leftDir;
                            else safeDirection = runAwayDir;
                        }
                        if (safeDirection == runAwayDir && distBack < 1.0f)
                            safeDirection = (distRight > distLeft) ? rightDir : leftDir;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}