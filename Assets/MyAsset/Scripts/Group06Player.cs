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
        static Vector3[] pos;
        static bool[] got;
        static int[] owner;
        static float[] ownerLen;

        static void Init()
        {
            n = GameManager.instance.NumTargets();
            pos = new Vector3[n];
            got = new bool[n];
            owner = new int[n];
            ownerLen = new float[n];
            for (int i = 0; i < n; i++)
            {
                pos[i] = GameManager.instance.TargetPosition(i);
                owner[i] = -1;
                ownerLen[i] = 1e30f;
            }
            usedMask = 0;
        }

        float PathLen(Vector3 a, Vector3 b)
        {
            if (!NavMesh.CalculatePath(a, b, NavMesh.AllAreas, path)) return 1e30f;
            if (path.status != NavMeshPathStatus.PathComplete) return 1e30f;

            var c = path.corners;
            if (c == null || c.Length <= 1) return 0f;

            float s = 0f;
            for (int i = 1; i < c.Length; i++) s += Vector3.Distance(c[i - 1], c[i]);
            return s;
        }

        void Start()
        {
            nav = GetNavMeshTool();
            path = new NavMeshPath();

            if (pos == null || n != GameManager.instance.NumTargets()) Init();

            id = (set == 0 || set == 1) ? set : 0;
            if ((usedMask & (1 << id)) != 0) id ^= 1; // 両方0でも分かれる
            usedMask |= 1 << id;

            Pick();
        }

        void Pick()
        {
            if (cur >= 0 && owner[cur] == id) { owner[cur] = -1; ownerLen[cur] = 1e30f; }

            Vector3 p = transform.position;
            float best = 1e30f;
            int besti = -1;

            const float stealEps = 0.1f; // 少しでも近い方が取る（ブレは最小限）

            for (int i = 0; i < n; i++)
            {
                if (got[i]) continue;

                float d = PathLen(p, pos[i]);
                int o = owner[i];

                // 他が同じターゲットを狙っていて、相手の方が十分近いなら避ける
                if (o != -1 && o != id && d >= ownerLen[i] - stealEps) continue;

                if (d < best) { best = d; besti = i; }
            }

            cur = besti;
            if (cur >= 0) { owner[cur] = id; ownerLen[cur] = best; }
            step = 0;
        }

        void Update()
        {
            if (pos == null) return;

            // ターゲットが取られた/奪われた/無効なら即リプラン
            if (cur < 0 || got[cur] || owner[cur] != id) Pick();
            if (cur < 0) { SetMoveSpeed(0f); return; }

            if (step == 0) { SetMoveSpeed(0f); nav.SetDestination(pos[cur]); step = 1; return; }
            if (step == 1) { if (!nav.IsReady()) return; step = 2; }

            nav.UpdateCurrentPosition();
            SetDirection(nav.MoveDirection());
            SetMoveSpeed(5f); // 速度固定なら内部で無視/クランプされるはず

            if (nav.IsArrived()) SetMoveSpeed(0f);
        }

        public override void TargetHit(int index)
        {
            if (index < 0 || index >= n) return;
            got[index] = true;
            owner[index] = -1;
            ownerLen[index] = 1e30f;
            if (cur == index) cur = -1; // 次フレーム即Pick
        }
    }
}
