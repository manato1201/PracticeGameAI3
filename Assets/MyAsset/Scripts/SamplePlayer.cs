using UnityEngine;
using System.Collections.Generic;

namespace sample
{
    public class SamplePlayer : Pawn
    {
        // 練習相手のグループ番号（敵とわかるように1に設定）
        int groupNo = 1;

        enum State
        {
            SEARCH_ITEM, // アイテムを探す
            CHASE,       // 敵を追いかけて攻撃
            ESCAPE       // 爆弾から逃げる
        }
        State currentState = State.SEARCH_ITEM;

        NavMeshTool navi;
        float thinkTimer = 0f;

        // 逃げる方向を保存する変数
        Vector3 escapeDir = Vector3.zero;

        void Start()
        {
            navi = GetNavMeshTool();
            SetGroupNo(groupNo);
        }

        void Update()
        {
            // 0.1秒ごとに思考する
            thinkTimer -= Time.deltaTime;
            if (thinkTimer < 0)
            {
                Think();
                thinkTimer = 0.1f;
            }

            // 現在の状態に合わせて行動する
            switch (currentState)
            {
                case State.SEARCH_ITEM:
                    DoSearchItem();
                    break;
                case State.CHASE:
                    DoChase();
                    break;
                case State.ESCAPE:
                    DoEscape();
                    break;
            }
        }

        // =========================================================
        // 思考ルーチン（ここが修正箇所！）
        // =========================================================
        void Think()
        {
            // 1. 【最優先】爆弾回避
            // ★以前のコードではここが抜けていました！
            if (CheckDanger(out escapeDir))
            {
                currentState = State.ESCAPE;
                return;
            }

            // 2. 【優先】敵を見つけたら追いかける
            GameObject enemy = FindEnemyInField();
            if (enemy != null)
            {
                // 敵との距離をチェック
                float dist = Vector3.Distance(GetPosition(), enemy.transform.position);

                // 敵が10メートル以内にいたらロックオン
                if (dist < 10.0f)
                {
                    currentState = State.CHASE;
                    return;
                }
            }

            // 3. 敵も危険もなければアイテムを集める
            currentState = State.SEARCH_ITEM;
        }

        // =========================================================
        // 行動ルーチン
        // =========================================================

        GameObject targetEnemy = null; // 追いかける対象

        void DoChase()
        {
            // ターゲットがいなくなったら探索に戻る
            if (targetEnemy == null || targetEnemy.GetComponent<Pawn>().IsDead())
            {
                currentState = State.SEARCH_ITEM;
                return;
            }

            // 敵の位置に向かって走る
            if (navi.IsReady())
            {
                navi.SetDestination(targetEnemy.transform.position);
                SetDirection(navi.MoveDirection());
                SetMoveSpeedRatio(1.0f);
            }

            // 敵の方を向く
            Vector3 dirToEnemy = targetEnemy.transform.position - GetPosition();
            SetDirection(dirToEnemy);

            // 射程内（6マス以内）なら撃つ
            if (dirToEnemy.magnitude < 6.0f)
            {
                ShootBomb();
            }
        }

        void DoSearchItem()
        {
            // 一番近いアイテムに向かう
            Vector3 targetPos = GetNearestItemPos();
            if (navi.IsReady())
            {
                navi.SetDestination(targetPos);
                SetDirection(navi.MoveDirection());
                SetMoveSpeedRatio(1.0f);
            }
        }

        void DoEscape()
        {
            // 【修正】手動移動(SetDirection)だと壁に擦るので、NavMeshを使う
            if (navi.IsReady())
            {
                // 逃げる方向の3メートル先を目的地にする
                Vector3 runTarget = GetPosition() + escapeDir * 3.0f;
                navi.SetDestination(runTarget);

                // NavMeshが計算した「角を曲がるための方向」を向く
                SetDirection(navi.MoveDirection());
                SetMoveSpeedRatio(1.0f);
            }
            else
            {
                // NavMeshが準備できてない時の予備（以前のコード）
                SetDirection(escapeDir);
                SetMoveSpeedRatio(1.0f);
            }
        }

        // =========================================================
        // センサー・ツール
        // =========================================================

        // フィールド全体の敵を探す
        GameObject FindEnemyInField()
        {
            for (int i = 0; i < cardinalDirectionNum; i++)
            {
                Vector3 dir = cardinalDirections[i];
                GameObject foundObj = null;
                if (CheckPlayer(dir, ref foundObj))
                {
                    Pawn p = foundObj.GetComponent<Pawn>();
                    if (p != null && p.GetTeamID() != GetTeamID())
                    {
                        targetEnemy = foundObj;
                        return foundObj;
                    }
                }
            }
            return targetEnemy;
        }

        // 曲がり角へ逃げ込む危険察知
        bool CheckDanger(out Vector3 safeDirection)
        {
            safeDirection = Vector3.zero;
            float dist = 0f;

            for (int i = 0; i < cardinalDirectionNum; i++)
            {
                Vector3 checkDir = cardinalDirections[i];

                // その方向に爆弾があるか？
                if (CheckBomb(checkDir, ref dist))
                {
                    // 4マス以内に爆弾があり、距離が近い
                    if (dist < 5.0f)
                    {
                        // 基本は「爆弾の反対側」へ
                        Vector3 runAwayDir = -checkDir;

                        // 左右（曲がり角）があるか調べる
                        Vector3 rightDir = Vector3.Cross(runAwayDir, Vector3.up);
                        Vector3 leftDir = -rightDir;

                        float distRight = DistanceToWall(rightDir);
                        float distLeft = DistanceToWall(leftDir);
                        float distBack = DistanceToWall(runAwayDir);

                        // 曲がり角判定
                        if (distRight > 1.5f)
                        {
                            safeDirection = rightDir;
                        }
                        else if (distLeft > 1.5f)
                        {
                            safeDirection = leftDir;
                        }
                        else
                        {
                            // 逃げ場がなければ後ろへ（壁なら横へあがく）
                            if (distBack < 1.0f)
                                safeDirection = (distRight > distLeft) ? rightDir : leftDir;
                            else
                                safeDirection = runAwayDir;
                        }
                        return true; // 危険あり
                    }
                }
            }
            return false; // 安全
        }

        Vector3 GetNearestItemPos()
        {
            int nearestIndex = -1;
            float minDist = float.MaxValue;
            for (int i = 0; i < GameManager.instance.NumItems(); i++)
            {
                if (GameManager.instance.IsItemAvailable(i))
                {
                    Vector3 p = GameManager.instance.ItemPosition(i);
                    float d = Vector3.Distance(GetPosition(), p);
                    if (d < minDist) { minDist = d; nearestIndex = i; }
                }
            }
            if (nearestIndex != -1) return GameManager.instance.ItemPosition(nearestIndex);
            return new Vector3(8, 0, 8);
        }
    }
}
