using UnityEngine;
using UnityEngine.AI;

namespace Group06
{
    public class Group06Player : Pawn
    {
        public int set = 0;

        NavMeshTool nav;
        NavMeshPath path;

        int id, cur = -1, step = 0;

        static int n = -1, usedMask = 0;
        static Vector3[] tpos;
        static bool[] got;
        static int[] area;                 // -1:未割当, 0/1:担当
        static bool splitDone = false;

        static Vector3[] startPos = new Vector3[2];
        static bool[] hasStart = new bool[2];

        static void InitTargets()
        {
            n = GameManager.instance.NumTargets();
            tpos = new Vector3[n];
            got = new bool[n];
            area = new int[n];
            for (int i = 0; i < n; i++)
            {
                tpos[i] = GameManager.instance.TargetPosition(i);
                area[i] = -1;
            }
            usedMask = 0;
            splitDone = false;
            hasStart[0] = hasStart[1] = false;
        }

        static float CalcLen(Vector3 a, Vector3 b, NavMeshPath p)
        {
            if (!NavMesh.CalculatePath(a, b, NavMesh.AllAreas, p)) return 1e30f;
            if (p.status != NavMeshPathStatus.PathComplete) return 1e30f;

            var c = p.corners;
            if (c == null || c.Length <= 1) return 0f;

            float s = 0f;
            for (int i = 1; i < c.Length; i++) s += Vector3.Distance(c[i - 1], c[i]);
            return s;
        }

        static void SplitAreasFast()
        {
            // 2*K 回のNavMesh.CalculatePathだけ（Kが多くてもロード1秒に収まりやすい）
            var p = new NavMeshPath();
            int cnt0 = 0, cnt1 = 0;

            for (int i = 0; i < n; i++)
            {
                float d0 = CalcLen(startPos[0], tpos[i], p);
                float d1 = CalcLen(startPos[1], tpos[i], p);

                int a;
                if (d0 >= 1e29f && d1 >= 1e29f) a = -1;        // どちらも到達不能
                else if (d1 >= 1e29f || d0 < d1) a = 0;
                else if (d0 >= 1e29f || d1 < d0) a = 1;
                else a = (cnt0 <= cnt1) ? 0 : 1;              // ほぼ同じなら数で均す

                area[i] = a;
                if (a == 0) cnt0++;
                else if (a == 1) cnt1++;
            }
            splitDone = true;
        }

        bool HasOwnRemaining()
        {
            if (!splitDone) return true;
            for (int i = 0; i < n; i++) if (!got[i] && area[i] == id) return true;
            return false;
        }

        void PickAndGo()
        {
            if (tpos == null) return;

            Vector3 p0 = transform.position;
            float best = 1e30f;
            int besti = -1;

            // pass0: 自分担当だけ
            // pass1: 自分担当が無ければ全部（ヘルプ）
            for (int pass = 0; pass < 2; pass++)
            {
                bool ownOnly = splitDone && (pass == 0) && HasOwnRemaining();
                for (int i = 0; i < n; i++)
                {
                    if (got[i]) continue;
                    if (ownOnly && area[i] != id) continue;

                    float d = CalcLen(p0, tpos[i], path);
                    if (d < best) { best = d; besti = i; }
                }
                if (besti != -1) break;
            }

            cur = besti;
            if (cur >= 0)
            {
                step = 0;
                nav.SetDestination(tpos[cur]); // ここで即目的地更新
                step = 1;
            }
        }

        void Start()
        {
            nav = GetNavMeshTool();
            path = new NavMeshPath();

            if (tpos == null || n != GameManager.instance.NumTargets()) InitTargets();

            id = (set == 1) ? 1 : 0;
            if ((usedMask & (1 << id)) != 0) id ^= 1; // 両方0でも分かれる
            usedMask |= 1 << id;

            startPos[id] = transform.position;
            hasStart[id] = true;

            // 2体揃った瞬間に“ロード時エリア分割”
            if (!splitDone && hasStart[0] && hasStart[1]) SplitAreasFast();

            PickAndGo();
        }

        void Update()
        {
            if (tpos == null) return;

            // ターゲットが消えた/自分担当が尽きた等なら即リプラン
            if (cur < 0 || got[cur]) PickAndGo();
            if (cur < 0) { SetMoveSpeed(0f); return; }

            // 経路計算待ち
            if (step == 1)
            {
                if (!nav.IsReady()) { SetMoveSpeed(0f); return; }
                step = 2;
            }

            // 移動
            nav.UpdateCurrentPosition();
            SetDirection(nav.MoveDirection());
            SetMoveSpeed(nav.IsArrived() ? 0f : 5f);
        }

        public override void TargetHit(int index)
        {
            if (index < 0 || index >= n) return;

            got[index] = true;

            // 触れた瞬間に次へ（ここがタイム短縮の核）
            if (cur == index) cur = -1;
            PickAndGo();
        }
    }
}
