using UnityEngine;

namespace miyashita
{

    public class Player_miyashita : Pawn
    {
        int groupNo = 6;
        bool lineEstablished = false;

        float moveTimer = 0f;
        float shootTimestamp = -10f;

        void Start()
        {
            SetGroupNo(groupNo);
        }

        void Update()
        {
            if (IsDead()) return;

            if (!lineEstablished)
            {
                AdvanceToNorth();
            }
            else
            {
                MaintainDefenseLine();
            }
        }

        void AdvanceToNorth()
        {
            float dist = DistanceToWall(Direction.N);

            if (dist > 0.5f)
            {
                SetDirection(Direction.N);
                SetMoveSpeedRatio(1f);
            }
            else
            {
                SetMoveSpeedRatio(0f);
                lineEstablished = true;
                moveTimer = 0f;
            }
        }

        void MaintainDefenseLine()
        {
            // 1. 爆弾回避を最優先（回避に成功した場合は以降の処理をスキップ）
            if (CheckAndEvadeSmart())
            {
                // 回避中はタイマーを進めない
                return;
            }

            // 2. 攻撃硬直チェック（downTime中はタイマーを停止）
            // Pawn.cs の animator.speed = 0.01f を利用して硬直を判定
            if (GetComponent<Animator>().speed < 0.1f)
            {
                return;
            }

            moveTimer += Time.deltaTime;

            // 6.0秒周期で罰則回避（往復時間を各0.4秒に増やし、確実に距離を稼ぐ）
            if (moveTimer > 2.0f)
            {
                if (moveTimer <= 2.3f)
                {
                    SetDirection(Direction.S);
                    SetMoveSpeedRatio(1f);
                }
                else if (moveTimer <= 2.6f)
                {
                    SetDirection(Direction.N);
                    SetMoveSpeedRatio(1f);
                }
                else
                {
                    SetMoveSpeedRatio(0f);
                    moveTimer = 0f;
                }
                return;
            }

            MonitorEnemies();
        }

        void MonitorEnemies()
        {
            GameObject enemy = null;
            foreach (var dir in cardinalDirections)
            {
                if (CheckPlayer(dir, ref enemy))
                {
                    if (enemy != null && enemy != GetFriend())
                    {
                        SetDirection(dir);
                        ShootBomb();
                        shootTimestamp = Time.time;
                        SetMoveSpeedRatio(0f);
                        return;
                    }
                }
            }
            SetMoveSpeedRatio(0f);
        }

        // 爆弾回避機能の強化版
        bool CheckAndEvadeSmart()
        {
            float bombDist = 0;
            // 足元および周囲の爆弾を検知
            if (CheckBomb(Vector3.zero, ref bombDist))
            {

                // 自分の爆弾（射撃直後）かつ至近距離なら無視して攻撃を優先
                if (Time.time - shootTimestamp < 1.0f && bombDist < 1.0f)
                {
                    return false;
                }

                // 2.5マス以内に爆弾がある場合、危険と判断
                if (bombDist < 2.5f)
                {
                    // 四方のうち、最も壁が遠い（逃げ道が広い）方向を選択
                    Vector3 bestDir = Direction.S;
                    float maxSpace = -1f;

                    foreach (var dir in cardinalDirections)
                    {
                        float d = DistanceToWall(dir);
                        if (d > maxSpace)
                        {
                            maxSpace = d;
                            bestDir = dir;
                        }
                    }

                    SetDirection(bestDir);
                    SetMoveSpeedRatio(1f);
                    return true; // 回避行動中フラグ
                }
            }
            return false;
        }
    }

}