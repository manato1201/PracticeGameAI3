using UnityEngine;

namespace Group03
{


    public class Group03Player : Pawn
    {
        private const int GROUP_NO = 3;

        private static readonly Vector3[] DIRS =
        {
            Vector3.forward,
            Vector3.back,
            Vector3.left,
            Vector3.right,
        };

        private NavMeshTool navi;
        private Group03Team team;
        private int myIndex = -1;

        // ---- キャッシュ更新（毎フレームはやらない） ----
        [SerializeField] private float senseInterval = 1.2f;
        private float senseTick = 0f;

        private Vector3 cachedEnemyPos = Vector3.zero;
        private bool hasEnemy = false;
        private int cachedEnemyIndex = -1;

        private Vector3 cachedMatePos = Vector3.zero;
        private bool hasMate = false;

        // ---- 行動パラメータ ----
        [SerializeField] private float keepRange = 5.2f; // この距離を維持（消極的）
        [SerializeField] private float tooClose = 3.6f; // ここより近いなら強制撤退
        [SerializeField] private float shootRangeMax = 7.0f; // これより遠いなら撃たない
        [SerializeField] private float alignEps = 0.6f; // 射線判定

        // 撃った直後の退避（自爆防止）
        private float postShotEvadeTimer = 0f;
        private Vector3 postShotEvadeDir = Vector3.zero;
        [SerializeField] private float postShotEvadeSec = 0.85f;

        // 爆弾回避：後退→横移動の2段階
        private float bombCheckTick = 0f;
        private Vector3 bombDir = Vector3.zero;
        private int bombState = 0; // 0:なし 1:後退 2:横へ
        private float bombStateTimer = 0f;

        // クールダウン
        private float shootCooldown = 0f;

        // 移動の安定化（角震え対策）
        private Vector3 smoothDir = Vector3.forward;

        void Start()
        {
            SetGroupNo(GROUP_NO);

            navi = GetNavMeshTool();
            team = GetComponentInParent<Group03Team>();
            if (team != null)
            {
                team.Register(this);
                myIndex = team.IndexOf(this);
            }

            // 個体差（全員同時前進で全滅を減らす）
            senseTick = Random.Range(0f, senseInterval * 0.5f);
            bombCheckTick = Random.Range(0f, 0.25f);
        }

        void Update()
        {
            if (shootCooldown > 0f) shootCooldown -= Time.deltaTime;

            Vector3 myPos = GetPosition();

            // 1.2秒ごとに情報更新（毎フレームはやらない）
            senseTick -= Time.deltaTime;
            if (senseTick <= 0f)
            {
                senseTick = senseInterval;
                UpdateSenseCache();
            }

            // 爆弾チェックも毎フレームやらない（暴れ防止）
            bombCheckTick -= Time.deltaTime;
            if (bombCheckTick <= 0f)
            {
                bombCheckTick = 0.20f;
                UpdateBombState();
            }

            // 1) 爆弾回避（最優先）
            if (bombState != 0)
            {
                DoBombEvade(myPos);
                return;
            }

            // 2) 撃った直後：必ず反対方向へ退避（自爆防止）
            if (postShotEvadeTimer > 0f)
            {
                postShotEvadeTimer -= Time.deltaTime;
                Vector3 dir = postShotEvadeDir;

                // 退避先が壁なら右優先で曲げる（右往左往させない）
                if (DistanceToWall(dir) < 0.9f)
                {
                    Vector3 right = new Vector3(dir.z, 0f, -dir.x);
                    if (DistanceToWall(right) >= DistanceToWall(-right)) dir = right;
                    else dir = -right;
                }

                MoveStable(dir);
                return;
            }

            // 3) 敵が居るなら「近づかず、射線が通れば撃って下がる」
            if (hasEnemy)
            {
                Vector3 toEnemy = cachedEnemyPos - myPos;
                toEnemy.y = 0f;
                float dist = toEnemy.magnitude;

                // 味方に近づかない（挟み撃ち維持）
                Vector3 mateRepel = Vector3.zero;
                if (hasMate)
                {
                    Vector3 d = myPos - cachedMatePos;
                    d.y = 0f;
                    float dsq = d.sqrMagnitude;
                    if (dsq < 3.0f * 3.0f && dsq > 0.0001f)
                        mateRepel = d.normalized * 0.75f;
                }

                // 射線に入った：撃つ → そのまま反対方向へ退避（逃げ撃ち）
                if (dist <= shootRangeMax && IsInSameLine(toEnemy) && CanShootWithoutWall(myPos, cachedEnemyPos))
                {
                    // 味方が近い/同射線なら撃たない（ただし敵が1体なら許容寄り）
                    bool allowRisk =
                        (team != null && team.EnemyAliveCount() <= 1);

                    if (allowRisk || !IsMateInBlastLine(myPos, cachedMatePos))
                    {
                        Vector3 shotDir = SnapToCardinal(toEnemy.normalized);
                        SetDirection(shotDir);
                        if (shootCooldown <= 0f)
                        {
                            ShootBomb();
                            shootCooldown = 1.0f;

                            // 撃った方向の反対へ退避（絶対）
                            postShotEvadeDir = -shotDir;
                            postShotEvadeTimer = postShotEvadeSec;
                        }
                    }

                    // 撃った後（もしくは撃てない時も）下がる
                    Vector3 retreat = (-toEnemy.normalized + mateRepel).normalized;
                    MoveStable(retreat);
                    return;
                }

                // 近すぎるなら撤退（消極的）
                if (dist < tooClose)
                {
                    Vector3 retreat = (-toEnemy.normalized + mateRepel).normalized;
                    MoveStable(retreat);
                    return;
                }

                // 遠い：無理に突っ込まず、フランク位置へ（挟み撃ち狙い）
                Vector3 goal = (team != null) ? team.GetFlankGoal(myIndex, myPos) : cachedEnemyPos;
                // ただし敵に近づきすぎないよう、最終的な進行方向は「距離維持」を混ぜる
                Vector3 toGoal = goal - myPos;
                toGoal.y = 0f;
                Vector3 dirToGoal = (toGoal.sqrMagnitude > 0.0001f) ? toGoal.normalized : Vector3.zero;

                // 距離維持（近づき過ぎ禁止）
                Vector3 keep = Vector3.zero;
                if (dist < keepRange) keep = (-toEnemy.normalized) * 0.7f;

                Vector3 move = (dirToGoal + keep + mateRepel).normalized;
                MoveStable(move);
                return;
            }

            // 4) 敵不明なら適当に中央寄り（突っ込まない）
            Vector3 idle = new Vector3(8f, 0f, 8f) - myPos;
            idle.y = 0f;
            if (idle.sqrMagnitude > 0.0001f)
                MoveStable(idle.normalized);
            else
                MoveStable(Vector3.forward);
        }

        // ---------------------------
        // 情報更新（1.2秒ごと）
        // ---------------------------
        private void UpdateSenseCache()
        {
            if (team == null && myIndex < 0)
                myIndex = (team != null) ? team.IndexOf(this) : -1;

            // 味方
            hasMate = false;
            cachedMatePos = Vector3.zero;
            if (team != null)
            {
                hasMate = team.TryGetMatePos(myIndex, out cachedMatePos);
            }

            // 敵（Teamが GameManager から引いてる）
            hasEnemy = false;
            cachedEnemyPos = Vector3.zero;
            cachedEnemyIndex = -1;
            if (team != null)
            {
                hasEnemy = team.TryGetAssignedEnemyPos(myIndex, out cachedEnemyPos, out cachedEnemyIndex);
            }
        }

        // ---------------------------
        // 爆弾状態更新（0.2秒ごと）
        // ---------------------------
        private void UpdateBombState()
        {
            float dist = float.PositiveInfinity;

            // 爆弾が見えたら「その方向」を記録
            // ※Pawn.CheckBomb は ref
            for (int i = 0; i < 4; i++)
            {
                float d = 0f;
                if (CheckBomb(DIRS[i], ref d))
                {
                    // 危険距離：保守的に
                    if (d < 3.3f)
                    {
                        bombDir = DIRS[i];
                        bombState = 1; // まず後退
                        bombStateTimer = 0.6f; // 少し下がる
                        return;
                    }
                }
            }

            // 爆弾なし
            bombState = 0;
            bombDir = Vector3.zero;
            bombStateTimer = 0f;
        }

        private void DoBombEvade(Vector3 myPos)
        {
            // bombDir 方向に爆弾がある → 逆方向へ後退
            Vector3 back = -bombDir;

            if (bombState == 1)
            {
                bombStateTimer -= Time.deltaTime;

                // まず後退（壁が無くなるまで）
                if (DistanceToWall(back) > 0.9f)
                    MoveStable(back);
                else
                    MoveStable(ChooseRightOrLeft(back)); // 後ろも壁なら曲げる

                // タイマーが切れたら「横へ」
                if (bombStateTimer <= 0f)
                {
                    bombState = 2;
                    bombStateTimer = 0.55f;
                }

                return;
            }

            if (bombState == 2)
            {
                bombStateTimer -= Time.deltaTime;

                // 横移動：右優先（左右迷いでクルクルしない）
                Vector3 side = ChooseRightOrLeft(bombDir);

                // 横が壁なら「さらに少し後退」してから横
                if (DistanceToWall(side) < 0.9f)
                {
                    MoveStable(back);
                }
                else
                {
                    MoveStable(side);
                }

                if (bombStateTimer <= 0f)
                {
                    bombState = 0;
                    bombDir = Vector3.zero;
                }

                return;
            }
        }

        // ---------------------------
        // 撃つ条件（壁越し撃ち禁止）
        // ---------------------------
        private bool CanShootWithoutWall(Vector3 myPos, Vector3 enemyPos)
        {
            // 壁越し撃ちは、爆弾が壁に当たって自爆率が上がるだけ
            // → Pawn.CheckPlayerで「その方向にPlayerがいる」= 壁で遮られてない可能性が高い
            Vector3 d = enemyPos - myPos;
            d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return false;

            Vector3 shotDir = Vector3.zero;
            if (Mathf.Abs(d.x) < alignEps)
                shotDir = (d.z >= 0f) ? Vector3.forward : Vector3.back;
            else if (Mathf.Abs(d.z) < alignEps)
                shotDir = (d.x >= 0f) ? Vector3.right : Vector3.left;
            else
                return false;

            GameObject hitObj = null;
            // CheckPlayerは「最初に当たったPlayer」だけ
            // ここで当たらないなら壁 or 空振りのどちらか → 撃たない
            if (!CheckPlayer(shotDir, ref hitObj)) return false;
            if (hitObj == null) return false;

            // 自分/味方ならダメ
            if (hitObj == gameObject) return false;
            if (GetFriend() != null && hitObj == GetFriend()) return false;

            return true;
        }

        private bool IsMateInBlastLine(Vector3 myPos, Vector3 matePos)
        {
            if (!hasMate) return false;

            Vector3 m = matePos - myPos;
            m.y = 0f;

            float near = 3.0f;
            if (m.sqrMagnitude < near * near) return true;

            float cross = 4.0f;
            if (Mathf.Abs(m.x) < 0.6f && Mathf.Abs(m.z) < cross) return true;
            if (Mathf.Abs(m.z) < 0.6f && Mathf.Abs(m.x) < cross) return true;

            return false;
        }

        // ---------------------------
        // 射線判定
        // ---------------------------
        private bool IsInSameLine(Vector3 toEnemy)
        {
            return (Mathf.Abs(toEnemy.x) < alignEps) || (Mathf.Abs(toEnemy.z) < alignEps);
        }

        // ---------------------------
        // 移動の安定化（角震えを減らす）
        // ---------------------------
        private void MoveStable(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;

            // 壁に向かうなら右優先で曲げる（迷わせない）
            if (DistanceToWall(dir) < 0.8f)
            {
                dir = ChooseRightOrLeft(dir);
            }

            // スムージング：角で小刻みに震えるのを抑える
            smoothDir = Vector3.Lerp(smoothDir, dir.normalized, 0.25f);
            smoothDir.y = 0f;
            if (smoothDir.sqrMagnitude < 0.0001f) smoothDir = dir.normalized;

            SetDirection(smoothDir.normalized);
            SetMoveSpeedRatio(1.0f); // Pawnは 0..1
        }

        // 「右優先」選択（同点で迷ってクルクルするのをやめさせる）
        private Vector3 ChooseRightOrLeft(Vector3 baseDir)
        {
            baseDir.y = 0f;
            if (baseDir.sqrMagnitude < 0.0001f) baseDir = Vector3.forward;

            Vector3 right = new Vector3(baseDir.z, 0f, -baseDir.x);
            Vector3 left = -right;

            float dr = DistanceToWall(right);
            float dl = DistanceToWall(left);

            // 迷ったら右
            if (Mathf.Abs(dr - dl) < 0.05f) return right;
            return (dr >= dl) ? right : left;
        }

        private Vector3 SnapToCardinal(Vector3 v)
        {
            v.y = 0f;
            if (Mathf.Abs(v.x) >= Mathf.Abs(v.z))
                return (v.x >= 0f) ? Vector3.right : Vector3.left;
            else
                return (v.z >= 0f) ? Vector3.forward : Vector3.back;
        }
    }
}
