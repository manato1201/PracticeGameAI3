using UnityEngine;

namespace Group05
{
    public class Group05Team : MonoBehaviour
    {
        private const int TEAM_PLAYERS = 2;

        private Pawn[] members = new Pawn[TEAM_PLAYERS];
        private int memberCount = 0;

        private TeamID myTeamId = TeamID.TeamA;
        private TeamID enemyTeamId = TeamID.TeamB;

        // それぞれが狙う敵のインデックスを Team が決めて配る
        private int[] assignedEnemy = new int[TEAM_PLAYERS] { 0, 1 };
        private float assignLockTimer = 0f;

        public void Register(Pawn p)
        {
            if (p == null) return;
            if (memberCount >= TEAM_PLAYERS) return;

            members[memberCount] = p;
            memberCount++;

            myTeamId = p.GetTeamID();
            enemyTeamId = (myTeamId == TeamID.TeamA) ? TeamID.TeamB : TeamID.TeamA;
        }

        public int IndexOf(Pawn p)
        {
            if (p == null) return -1;
            for (int i = 0; i < TEAM_PLAYERS; i++)
            {
                if (members[i] == p) return i;
            }
            return -1;
        }

        public Pawn GetMate(int myIndex)
        {
            int mateIndex = (myIndex == 0) ? 1 : 0;
            if (mateIndex < 0 || mateIndex >= TEAM_PLAYERS) return null;
            return members[mateIndex];
        }

        public Vector3 GetMatePosition(int myIndex)
        {
            var mate = GetMate(myIndex);
            if (mate == null) return Vector3.zero;
            return mate.GetPosition();
        }

        public int EnemyAliveCount()
        {
            var gm = GameManager.instance;
            if (gm == null) return 0;

            int alive = 0;
            if (!gm.IsPlayerDead(enemyTeamId, 0)) alive++;
            if (!gm.IsPlayerDead(enemyTeamId, 1)) alive++;
            return alive;
        }

        // Teamが「担当敵」を決める（2体生存なら分担、1体なら同じ）
        public int GetAssignedEnemyIndex(int myIndex, Vector3 myPos)
        {
            var gm = GameManager.instance;
            if (gm == null) return 0;

            bool e0Alive = !gm.IsPlayerDead(enemyTeamId, 0);
            bool e1Alive = !gm.IsPlayerDead(enemyTeamId, 1);

            // 敵が1体以下なら全員その敵
            if (e0Alive && !e1Alive) return 0;
            if (!e0Alive && e1Alive) return 1;
            if (!e0Alive && !e1Alive) return 0;

            // 2体いる場合：ロック時間内は担当固定（フリップ防止）
            assignLockTimer -= Time.deltaTime;
            if (assignLockTimer > 0f)
                return assignedEnemy[Mathf.Clamp(myIndex, 0, 1)];

            // 再割当（短周期でやると不安定になるのでロックする）
            Vector3 e0 = gm.GetPlayerPosition(enemyTeamId, 0);
            Vector3 e1 = gm.GetPlayerPosition(enemyTeamId, 1);

            // 基本方針：
            // - member0 が近い方を取る
            // - member1 は残りを取る
            // ※ これで「同じ対象に吸われる」を潰す
            float d0 = (e0 - myPos).sqrMagnitude;
            float d1 = (e1 - myPos).sqrMagnitude;

            int m0 = (d0 <= d1) ? 0 : 1;
            int m1 = 1 - m0;

            assignedEnemy[0] = m0;
            assignedEnemy[1] = m1;

            assignLockTimer = 0.50f; // 半秒固定（グルグル対策）

            return assignedEnemy[Mathf.Clamp(myIndex, 0, 1)];
        }

        public Vector3 GetEnemyPositionByAssigned(int myIndex, Vector3 myPos)
        {
            var gm = GameManager.instance;
            if (gm == null) return Vector3.zero;

            int idx = GetAssignedEnemyIndex(myIndex, myPos);
            return gm.GetPlayerPosition(enemyTeamId, idx);
        }

        // 攻撃ゴール（担当敵）
        public Vector3 GetAttackGoal(int myIndex, Vector3 myPos)
        {
            Vector3 enemyPos = GetEnemyPositionByAssigned(myIndex, myPos);
            if (enemyPos == Vector3.zero)
                return new Vector3(8f, 0f, 8f);

            return enemyPos;
        }
    }
}
