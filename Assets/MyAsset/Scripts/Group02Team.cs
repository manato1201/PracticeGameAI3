using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Group02
{
    public class Group02Team : MonoBehaviour
    {
        public List<Pawn> enemies = new List<Pawn>();

        void Start()
        {
            // フィールド上の全Pawnから敵チームを抽出
            Pawn[] allPawns = Object.FindObjectsByType<Pawn>(FindObjectsSortMode.None);
            Pawn myMember = GetComponentInChildren<Pawn>();
            if (myMember == null) return;
            TeamID myTeam = myMember.GetTeamID();

            foreach (Pawn p in allPawns)
            {
                if (p.GetTeamID() != myTeam && p.GetTeamID() != TeamID.TeamZ)
                {
                    enemies.Add(p);
                }
            }
        }

        public Pawn GetTargetEnemy(Vector3 myPos)
        {
            Pawn bestTarget = null;
            float minDir = float.MaxValue;
            foreach (var e in enemies)
            {
                if (e != null && !e.IsDead())
                {
                    float d = Vector3.Distance(myPos, e.GetPosition());
                    if (d < minDir)
                    {
                        minDir = d;
                        bestTarget = e;
                    }
                }
            }
            return bestTarget;
        }
    }
}
