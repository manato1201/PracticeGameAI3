using UnityEngine;

namespace Group05
{
    // Team_A / Team_B にアタッチ想定（子に Chara_1, Chara_2）
    public class Group05Team : MonoBehaviour
    {
        public const int GridMin = 1;
        public const int GridMax = 15;
        private const int GridSize = GridMax + 1;  // 0..15
        private const int NodeCount = GridSize * GridSize;

        [Header("Tuning")]
        [SerializeField] private float senseInterval = 0.5f;
        [SerializeField] private float wallRebuildInterval = 10f;

        private LayerMask wallMask;
        private bool[,] walkable = new bool[GridSize, GridSize];

        private float nextSenseTime = -1f;
        private float nextWallRebuildTime = -1f;

        private TeamID myTeam = TeamID.TeamZ;
        private TeamID enemyTeam = TeamID.TeamZ;

        private readonly Vector3[] friendPos = new Vector3[2];
        private readonly bool[] friendAlive = new bool[2];
        private readonly Vector3[] enemyPos = new Vector3[2];
        private readonly bool[] enemyAlive = new bool[2];

        // A*（割り当て抑制）
        private readonly float[] gCost = new float[NodeCount];
        private readonly float[] fCost = new float[NodeCount];
        private readonly int[] cameFrom = new int[NodeCount];
        private readonly bool[] closed = new bool[NodeCount];
        private readonly int[] open = new int[NodeCount];

        void Awake()
        {
            wallMask = LayerMask.GetMask("Wall");

            Team t = GetComponent<Team>();
            if (t != null)
            {
                myTeam = t.teamId;
                enemyTeam = (myTeam == TeamID.TeamA) ? TeamID.TeamB
                          : (myTeam == TeamID.TeamB) ? TeamID.TeamA
                          : TeamID.TeamZ;
            }

            BuildWalkable();
            UpdatePerception();

            nextWallRebuildTime = Time.time + wallRebuildInterval;
            nextSenseTime = Time.time + senseInterval;
        }

        void Update()
        {
            float now = Time.time;

            if (now >= nextWallRebuildTime)
            {
                BuildWalkable();
                nextWallRebuildTime = now + wallRebuildInterval;
            }

            if (now >= nextSenseTime)
            {
                UpdatePerception();
                nextSenseTime = now + senseInterval;
            }
        }

        private void UpdatePerception()
        {
            if (GameManager.instance == null) return;

            for (int i = 0; i < 2; i++)
            {
                friendAlive[i] = !GameManager.instance.IsPlayerDead(myTeam, i);
                friendPos[i] = GameManager.instance.GetPlayerPosition(myTeam, i);
            }

            for (int i = 0; i < 2; i++)
            {
                enemyAlive[i] = !GameManager.instance.IsPlayerDead(enemyTeam, i);
                enemyPos[i] = GameManager.instance.GetPlayerPosition(enemyTeam, i);
            }
        }

        public bool TryGetFriend(int index, out Vector3 pos, out bool alive)
        {
            if (index < 0 || index >= 2)
            {
                pos = Vector3.zero;
                alive = false;
                return false;
            }
            pos = friendPos[index];
            alive = friendAlive[index];
            return true;
        }

        public bool TryGetEnemy(int index, out Vector3 pos, out bool alive)
        {
            if (index < 0 || index >= 2)
            {
                pos = Vector3.zero;
                alive = false;
                return false;
            }
            pos = enemyPos[index];
            alive = enemyAlive[index];
            return true;
        }

        // -------------------------
        // Wall grid
        // -------------------------
        private void BuildWalkable()
        {
            Vector3 half = new Vector3(0.45f, 0.6f, 0.45f);
            Quaternion rot = Quaternion.identity;

            for (int x = 0; x <= GridMax; x++)
                for (int z = 0; z <= GridMax; z++)
                    walkable[x, z] = false;

            for (int x = GridMin; x <= GridMax; x++)
            {
                for (int z = GridMin; z <= GridMax; z++)
                {
                    Vector3 center = new Vector3(x, 0.5f, z);
                    bool hitWall = Physics.CheckBox(center, half, rot, wallMask);
                    walkable[x, z] = !hitWall;
                }
            }
        }

        public bool IsWalkable(Vector3Int cell)
        {
            if (cell.x < GridMin || cell.x > GridMax || cell.z < GridMin || cell.z > GridMax)
                return false;
            return walkable[cell.x, cell.z];
        }

        public Vector3Int WorldToCell(Vector3 world)
        {
            return new Vector3Int(Mathf.RoundToInt(world.x), 0, Mathf.RoundToInt(world.z));
        }

        public Vector3 CellToWorld(Vector3Int cell)
        {
            return new Vector3(cell.x, 0f, cell.z);
        }

        // -------------------------
        // A*（小規模グリッドなので線形 open で十分）
        // -------------------------
        public bool TryGetNextStepOnGrid(Vector3 startWorld, Vector3 goalWorld, out Vector3 nextWorld)
        {
            Vector3Int start = WorldToCell(startWorld);
            Vector3Int goal = WorldToCell(goalWorld);

            nextWorld = startWorld;

            if (!IsWalkable(start) || !IsWalkable(goal))
                return false;

            if (start == goal)
            {
                nextWorld = CellToWorld(start);
                return true;
            }

            int startIdx = ToIndex(start.x, start.z);
            int goalIdx = ToIndex(goal.x, goal.z);

            for (int i = 0; i < NodeCount; i++)
            {
                gCost[i] = float.PositiveInfinity;
                fCost[i] = float.PositiveInfinity;
                cameFrom[i] = -1;
                closed[i] = false;
            }

            int openCount = 0;
            gCost[startIdx] = 0f;
            fCost[startIdx] = Heuristic(start, goal);
            open[openCount++] = startIdx;

            while (openCount > 0)
            {
                int best = 0;
                float bestF = fCost[open[0]];
                for (int i = 1; i < openCount; i++)
                {
                    int idx = open[i];
                    float f = fCost[idx];
                    if (f < bestF)
                    {
                        bestF = f;
                        best = i;
                    }
                }

                int current = open[best];
                open[best] = open[openCount - 1];
                openCount--;

                if (current == goalIdx)
                {
                    int step = current;
                    int prev = cameFrom[step];
                    while (prev != -1 && prev != startIdx)
                    {
                        step = prev;
                        prev = cameFrom[step];
                    }

                    nextWorld = CellToWorld(IndexToCell(step));
                    return true;
                }

                closed[current] = true;

                Vector3Int c = IndexToCell(current);
                TryRelaxNeighbor(goal, current, c.x + 1, c.z, ref openCount);
                TryRelaxNeighbor(goal, current, c.x - 1, c.z, ref openCount);
                TryRelaxNeighbor(goal, current, c.x, c.z + 1, ref openCount);
                TryRelaxNeighbor(goal, current, c.x, c.z - 1, ref openCount);
            }

            return false;
        }

        private void TryRelaxNeighbor(Vector3Int goal, int currentIdx, int nx, int nz, ref int openCount)
        {
            if (nx < GridMin || nx > GridMax || nz < GridMin || nz > GridMax)
                return;

            if (!walkable[nx, nz])
                return;

            int nIdx = ToIndex(nx, nz);
            if (closed[nIdx])
                return;

            float newG = gCost[currentIdx] + 1f;
            if (newG < gCost[nIdx])
            {
                cameFrom[nIdx] = currentIdx;
                gCost[nIdx] = newG;
                fCost[nIdx] = newG + Heuristic(new Vector3Int(nx, 0, nz), goal);

                bool exists = false;
                for (int i = 0; i < openCount; i++)
                {
                    if (open[i] == nIdx)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    open[openCount++] = nIdx;
                }
            }
        }

        private float Heuristic(Vector3Int a, Vector3Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.z - b.z);
        }

        private int ToIndex(int x, int z) => x * GridSize + z;

        private Vector3Int IndexToCell(int idx)
        {
            int x = idx / GridSize;
            int z = idx - x * GridSize;
            return new Vector3Int(x, 0, z);
        }
    }
}
