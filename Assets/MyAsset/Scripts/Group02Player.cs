using UnityEngine;
using UnityEngine.AI;

namespace Group02
{
    public class Group02Player : Pawn
    {
        NavMeshTool navi;
        Group02Team team;

        // 状態をシンプルに：移動して攻撃するか、10秒死を避けるか
        enum State { CHASE_AND_ATTACK, FORCED_MOVE }
        State state = State.CHASE_AND_ATTACK;

        Vector3 lastCheckPos;
        float stopTimer = 0f;
        private int wallLayerMask;

        void Start()
        {
            navi = GetNavMeshTool();
            SetGroupNo(2);
            team = GetComponentInParent<Group02Team>();
            lastCheckPos = GetPosition();

            // 壁判定用のレイヤー（Unityエディタで壁を"Wall"レイヤーに設定してください）
            wallLayerMask = LayerMask.GetMask("Wall");
        }

        void Update()
        {
            if (IsDead()) return;

            // --- 1. 停止時間の計測 (10秒ルール対策) ---
            if (Vector3.Distance(GetPosition(), lastCheckPos) < 0.1f)
            {
                stopTimer += Time.deltaTime;
            }
            else
            {
                stopTimer = 0f;
                lastCheckPos = GetPosition();
            }

            // --- 2. 状態の決定 ---
            // 7秒以上止まっていたら、強制的に「中央へ移動」して死を回避
            if (stopTimer > 7.0f)
                state = State.FORCED_MOVE;
            else
                state = State.CHASE_AND_ATTACK;

            // --- 3. 行動実行 ---
            switch (state)
            {
                case State.CHASE_AND_ATTACK:
                    HandleChaseAndAttack();
                    break;
                case State.FORCED_MOVE:
                    HandleForcedMove();
                    break;
            }
        }

        void HandleChaseAndAttack()
        {
            Pawn target = team.GetTargetEnemy(GetPosition());
            if (target == null) return;

            Vector3 targetPos = target.GetPosition();

            // 敵に向かって移動（NavMeshによる経路移動）
            // これにより、壁を避けながら最短で近づきます
            MoveTo(targetPos);

            // 「軸が合っている」かつ「射線に壁がない」なら爆弾を投げる
            if (IsOnStraightLine(targetPos) && !IsWallInWay(targetPos))
            {
                // 敵の方向を向いて発射
                SetDirection(targetPos - GetPosition());
                ShootBomb();
            }
        }

        void HandleForcedMove()
        {
            // ステージ中央へ移動して停止時間をリセットする
            MoveTo(new Vector3(8.0f, 0, 8.0f));
        }

        // --- 共通移動関数 ---
        void MoveTo(Vector3 goal)
        {
            navi.SetDestination(goal);
            Vector3 nextDir = navi.MoveDirection();
            if (nextDir.magnitude > 0.1f)
            {
                SetDirection(nextDir);
                SetMoveSpeedRatio(1.0f);
            }
            navi.UpdateCurrentPosition();
        }

        // --- 判定関数（レイヤー使用版） ---
        bool IsWallInWay(Vector3 targetPos)
        {
            Vector3 origin = GetPosition() + Vector3.up * 0.5f;
            Vector3 diff = targetPos - GetPosition();
            // 壁レイヤーのみを対象にレイを飛ばす
            return Physics.Raycast(origin, diff.normalized, diff.magnitude, wallLayerMask);
        }

        bool IsOnStraightLine(Vector3 target)
        {
            Vector3 diff = target - GetPosition();
            // X軸かZ軸のズレが0.5m以内なら「軸が合っている」とみなす
            return Mathf.Abs(diff.x) < 0.5f || Mathf.Abs(diff.z) < 0.5f;
        }
    }
}
