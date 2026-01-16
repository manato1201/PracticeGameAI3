using UnityEngine;
using System.Collections.Generic;

namespace Group06
{
    public class Group06Player : Pawn
    {
        int groupNo = 6;
        NavMeshTool navi;

        float lastShootTime = -10f;
        const float downTimeLimit = 3.5f;
        const float safetyDistance = 1.1f;

        bool isAttacker = false;

        void Start()
        {
            navi = GetNavMeshTool();
            SetGroupNo(groupNo);

            Pawn[] mates = transform.parent.GetComponentsInChildren<Pawn>();
            for (int i = 0; i < mates.Length; i++)
            {
                if (mates[i] == this)
                {
                    isAttacker = (i == 0);
                    break;
                }
            }
        }

        void Update()
        {
            if (IsDead()) return;

            if (IsUnderThreat())
            {
                MoveToSafety();
                return;
            }

            Pawn target = GetNearestEnemy();
            if (isAttacker)
            {
                ExecuteUltimateAttacker(target);
            }
            else
            {
                ExecuteUltimateSupport(target);
            }
        }

        void ExecuteUltimateAttacker(Pawn target)
        {
            if (target == null)
            {
                MoveToPosition(new Vector3(8, 0, 8));
                return;
            }

            Vector3 shootDir = GetAttackDirection(target.transform.position);
            GameObject found = null;

            // 射線が通り、かつ自爆しない距離なら攻撃
            if (CheckPlayer(shootDir, ref found) && Vector3.Distance(transform.position, target.transform.position) < 6f)
            {
                if (DistanceToWall(shootDir) > safetyDistance && Time.time - lastShootTime > downTimeLimit + 0.1f)
                {
                    SetDirection(shootDir);
                    ShootBomb();
                    lastShootTime = Time.time;
                    return;
                }
            }

            // 攻撃チャンス以外は、常にターゲットの正面を維持するように動く
            MoveToPosition(target.transform.position);
        }

        void ExecuteUltimateSupport(Pawn target)
        {
            // アイテム回収を最優先して火力を強化
            int itemIdx = GetNearestItemIndex();
            if (itemIdx != -1)
            {
                MoveToPosition(GameManager.instance.ItemPosition(itemIdx));
                return;
            }

            if (target != null)
            {
                // アタッカーとは逆の方向（背後）から追い詰める
                Pawn attacker = GetAlly();
                if (attacker != null)
                {
                    Vector3 sandwichPos = target.transform.position + (target.transform.position - attacker.transform.position).normalized * 2.0f;
                    MoveToPosition(sandwichPos);

                    // 敵が自分とアタッカーの間にいるなら爆破
                    Vector3 shootDir = GetAttackDirection(target.transform.position);
                    if (Time.time - lastShootTime > downTimeLimit + 0.5f)
                    {
                        ShootBomb();
                        lastShootTime = Time.time;
                    }
                }
            }
        }

        Pawn GetAlly()
        {
            Pawn[] mates = transform.parent.GetComponentsInChildren<Pawn>();
            foreach (var m in mates)
            {
                if (m != this && !m.IsDead()) return m;
            }
            return null;
        }

        void MoveToPosition(Vector3 dest)
        {
            navi.SetDestination(dest);
            if (navi.IsReady())
            {
                SetDirection(navi.MoveDirection());
                SetMoveSpeedRatio(1.0f);
            }
        }

        Vector3 GetAttackDirection(Vector3 pos)
        {
            Vector3 diff = pos - transform.position;
            if (Mathf.Abs(diff.x) > Mathf.Abs(diff.z)) return diff.x > 0 ? Direction.E : Direction.W;
            return diff.z > 0 ? Direction.N : Direction.S;
        }

        Pawn GetNearestEnemy()
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            Pawn nearest = null;
            float minDist = float.MaxValue;
            foreach (GameObject obj in players)
            {
                Pawn p = obj.GetComponent<Pawn>();
                if (p != null && p.GetTeamID() != GetTeamID() && !p.IsDead())
                {
                    float d = Vector3.Distance(transform.position, p.transform.position);
                    if (d < minDist) { minDist = d; nearest = p; }
                }
            }
            return nearest;
        }

        int GetNearestItemIndex()
        {
            int nearest = -1;
            float minDist = float.MaxValue;
            for (int i = 0; i < GameManager.instance.NumItems(); i++)
            {
                if (GameManager.instance.IsItemAvailable(i))
                {
                    float d = Vector3.Distance(transform.position, GameManager.instance.ItemPosition(i));
                    if (d < minDist) { minDist = d; nearest = i; }
                }
            }
            return nearest;
        }

        bool IsUnderThreat()
        {
            GameObject[] bombs = GameObject.FindGameObjectsWithTag("Bomb");
            foreach (var b in bombs)
            {
                if (b == null) continue;
                Vector3 offset = transform.position - b.transform.position;
                if (Mathf.Abs(offset.x) < 0.8f && Mathf.Abs(offset.z) < 4.2f) return true;
                if (Mathf.Abs(offset.z) < 0.8f && Mathf.Abs(offset.x) < 4.2f) return true;
            }
            return false;
        }

        void MoveToSafety()
        {
            Vector3[] dirs = { Direction.N, Direction.S, Direction.E, Direction.W };
            foreach (Vector3 dir in dirs)
            {
                if (DistanceToWall(dir) > 2.5f)
                {
                    SetDirection(dir);
                    SetMoveSpeedRatio(1.0f);
                    return;
                }
            }
        }
    }
}