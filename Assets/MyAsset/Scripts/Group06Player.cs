using UnityEngine;
using UnityEngine.AI;

namespace Group06
{
    public class Group06Player : Pawn
    {
        [SerializeField] private float moveSpeed = 10f;
        [SerializeField] private float stuckTimeLimit = 0.6f;

        private NavMeshTool navi;
        private Group06TeamCoordinator coordinator;

        private int agentId = -1;
        private int currentTarget = -1;

        private int step = 0;
        private Vector3 lastPos;
        private float stuckTimer = 0f;

        // ★フィールド初期化で new しない（Unity6で禁止）
        private NavMeshPath checkPath;

        void Start()
        {
            // Pawn.Awake() が先に走って navi が作られている前提
            navi = GetNavMeshTool();

            // ★ここで生成（OK）
            checkPath = new NavMeshPath();

            lastPos = transform.position;
        }

        void Update()
        {
            if (coordinator == null) coordinator = GetComponentInParent<Group06TeamCoordinator>();
            if (coordinator == null || !coordinator.IsActive || agentId < 0) { SetMoveSpeed(0); return; }

            // 目的地が無い or 消えた なら次を貰う
            if (currentTarget < 0 || !coordinator.IsTargetStillActive(currentTarget))
            {
                currentTarget = coordinator.GetNextTarget(agentId);
                if (currentTarget < 0) { SetMoveSpeed(0); return; }

                Vector3 goal = coordinator.GetTargetPosition(currentTarget);
                navi.SetDestination(goal);
                // ここで止めない
            }

            if (!navi.IsReady()) { SetMoveSpeed(moveSpeed); return; } // pathPending中も走らせてOK

            Vector3 dir = navi.MoveDirection();
            if (dir.sqrMagnitude < 0.0001f)
            {
                // 詰んだら次へ（Team側にRejectTarget用意してるなら呼ぶ）
                coordinator.RejectTarget(agentId, currentTarget);
                currentTarget = -1;
                SetMoveSpeed(moveSpeed);
                return;
            }

            SetDirection(dir);
            SetMoveSpeed(moveSpeed);


        }

        private bool IsReachable(Vector3 start, Vector3 goal)
        {
            // 念のためnullガード
            if (checkPath == null) checkPath = new NavMeshPath();

            start.y = 0f; goal.y = 0f;
            if (!NavMesh.CalculatePath(start, goal, NavMesh.AllAreas, checkPath))
                return false;

            return checkPath.status == NavMeshPathStatus.PathComplete;
        }

        public void SetCoordinator(Group06TeamCoordinator c, int id)
        {
            coordinator = c;
            agentId = id;
        }

        public override void TargetHit(int index)
        {
            coordinator?.NotifyTargetHit(agentId, index);
            if (currentTarget == index) currentTarget = -1;
        }
    }
}
