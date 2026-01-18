using UnityEngine;

namespace Group03
{


    public class Group03Team : MonoBehaviour
    {
        [SerializeField] private float updateInterval = 1.2f;

        private Pawn[] members = new Pawn[2];
        private Vector3[] memberPos = new Vector3[2];
        private bool[] memberAlive = new bool[2];

        private Vector3[] enemyPos = new Vector3[2];
        private bool[] enemyAlive = new bool[2];
        private int enemyAliveCount = 0;

        private int[] assignedEnemy = new int[2] { -1, -1 };

        private float tick = 0f;

        private TeamID myTeam = TeamID.TeamZ;
        private TeamID enemyTeam = TeamID.TeamZ;

        void Awake()
        {
            // Team_A / Team_B のオブジェクト名から判定（手動設定不要）
            if (name.Contains("Team_A")) myTeam = TeamID.TeamA;
            else if (name.Contains("Team_B")) myTeam = TeamID.TeamB;

            enemyTeam = (myTeam == TeamID.TeamA) ? TeamID.TeamB : TeamID.TeamA;

            // 開始直後の暴れを減らすため、tickに個体差をつける
            tick = Random.Range(0f, updateInterval * 0.5f);
        }

        void Update()
        {
            tick -= Time.deltaTime;
            if (tick > 0f) return;
            tick = updateInterval;

            RefreshCache();
        }

        private void RefreshCache()
        {
            // メンバー位置更新
            for (int i = 0; i < 2; i++)
            {
                Pawn p = members[i];
                if (p != null && !p.IsDead())
                {
                    memberAlive[i] = true;
                    memberPos[i] = p.GetPosition();
                }
                else
                {
                    memberAlive[i] = false;
                    memberPos[i] = Vector3.zero;
                }
            }

            // 敵位置更新（GameManagerから）
            enemyAliveCount = 0;
            for (int e = 0; e < 2; e++)
            {
                bool dead = GameManager.instance.IsPlayerDead(enemyTeam, e);
                enemyAlive[e] = !dead;
                enemyPos[e] = GameManager.instance.GetPlayerPosition(enemyTeam, e);
                if (enemyAlive[e]) enemyAliveCount++;
            }

            // 割り当て更新：両敵生存なら「総距離が短い組み合わせ」を選ぶ
            // 片方だけなら2人とも同じ敵
            AssignEnemies();
        }

        private void AssignEnemies()
        {
            assignedEnemy[0] = -1;
            assignedEnemy[1] = -1;

            if (enemyAliveCount <= 0)
                return;

            if (enemyAliveCount == 1)
            {
                int idx = enemyAlive[0] ? 0 : 1;
                assignedEnemy[0] = idx;
                assignedEnemy[1] = idx;
                return;
            }

            // 敵2体生存：2通りの割り当てを評価
            // A: (0->0, 1->1), B: (0->1, 1->0)
            Vector3 p0 = memberPos[0];
            Vector3 p1 = memberPos[1];
            Vector3 e0 = enemyPos[0];
            Vector3 e1 = enemyPos[1];

            float a = (p0 - e0).sqrMagnitude + (p1 - e1).sqrMagnitude;
            float b = (p0 - e1).sqrMagnitude + (p1 - e0).sqrMagnitude;

            if (a <= b)
            {
                assignedEnemy[0] = 0;
                assignedEnemy[1] = 1;
            }
            else
            {
                assignedEnemy[0] = 1;
                assignedEnemy[1] = 0;
            }
        }

        public void Register(Pawn p)
        {
            if (p == null) return;

            for (int i = 0; i < 2; i++)
            {
                if (members[i] == null)
                {
                    members[i] = p;
                    return;
                }
            }
        }

        public int IndexOf(Pawn p)
        {
            if (p == null) return -1;
            if (members[0] == p) return 0;
            if (members[1] == p) return 1;
            return -1;
        }

        public Pawn GetMate(int myIndex)
        {
            int other = (myIndex == 0) ? 1 : 0;
            return members[other];
        }

        public bool TryGetMatePos(int myIndex, out Vector3 matePos)
        {
            matePos = Vector3.zero;
            int other = (myIndex == 0) ? 1 : 0;
            if (!memberAlive[other]) return false;
            matePos = memberPos[other];
            return true;
        }

        public bool TryGetAssignedEnemyPos(int myIndex, out Vector3 pos, out int enemyIndex)
        {
            pos = Vector3.zero;
            enemyIndex = -1;

            if (myIndex < 0 || myIndex > 1) return false;

            int idx = assignedEnemy[myIndex];
            if (idx < 0 || idx > 1) return false;
            if (!enemyAlive[idx]) return false;

            pos = enemyPos[idx];
            enemyIndex = idx;
            return true;
        }

        public int EnemyAliveCount()
        {
            return enemyAliveCount;
        }

        // 挟み撃ち用の「横取り位置」：敵の周りを左右にずらす
        public Vector3 GetFlankGoal(int myIndex, Vector3 myPos)
        {
            if (!TryGetAssignedEnemyPos(myIndex, out var epos, out _))
            {
                // 敵不明なら中央寄り（適当）
                return new Vector3(8f, 0f, 8f);
            }

            Vector3 toEnemy = epos - myPos;
            toEnemy.y = 0f;
            if (toEnemy.sqrMagnitude < 0.0001f)
                return epos;

            // 敵への方向の「直交」をフランク方向にする
            Vector3 perp = new Vector3(toEnemy.z, 0f, -toEnemy.x);
            perp.Normalize();

            // 2体で左右に分ける（0:右、1:左）
            Vector3 side = (myIndex == 0) ? perp : -perp;

            float flankDist = 3.2f;
            Vector3 goal = epos + side * flankDist;
            goal.y = 0f;
            return goal;
        }
    }
}
