/*
	授業のグループ番号をセットしてください
	void SetGroupNo(int no)
	
	#### 定義
	フィールド上の4方向（東西南北）
	public class Direction {
		public static Vector3 N = new Vector3(0f,0f,1f);
		public static Vector3 E = new Vector3(1f,0f,0f);
		public static Vector3 S = new Vector3(0f,0f,-1f);
		public static Vector3 W = new Vector3(-1f,0f,0f);
	}
	
	上記4方向の配列
	protected Vector3[] cardinalDirections = {
		Direction.N,
		Direction.E,
		Direction.S,
		Direction.W,
	};
	public const int cardinalDirectionNum = 4;
	
	参考：上記の配列で、キャラクターが n 番目方向を向いている場合、
		int Loop03(int n){ return (n+4)%4; }
		右： cardinalDirections[Loop03(n+1)]
		左： cardinalDirections[Loop03(n-1)]
		後： cardinalDirections[Loop03(n+2)]

	#### 情報
	
	経路探索機能を提供するコンポーネントを取得します
	NavMeshTool GetNavMeshTool()

	位置を取得する
	Vector3 GetPosition()
	
	小数点なしの整数単位に丸め込んだ位置を取得する
	Vector3Int GetIntPosition()
	
	向きを取得する
	Vector3 GetDirection()
	
	自分の爆弾レベルを取得する
	初期値は１
	void GetBombLevel()
	
	同じチームの仲間プレイヤーの GameObject を取得する
	public GameObject GetFriend()
	
	グループ番号を返す
	int GetGroupNo()
	
	所属するチームを返す（TeamID.TeamA または TeamID.TeamB）
	TeamID GetTeamID()
	
	#### 制御 
	
	向きを設定する
	向きはxz要素のみの単位ベクトルに補正される
	void SetDirection(Vector3)
	
	移動速度の割合を設定する
	0:停止 ~ 1:最大速度
	void SetMoveSpeedRatio(float)
	
	向いている方向に爆弾を投げる
	ただし、爆弾を投げる方向は東西南北の4方向に補正される
	実行後、2.5秒間なにもできなくなる
	void ShootBomb()
	
	#### 周囲のチェック
	
	float DistanceToWall(Vector3)
		引数方向の壁までの距離を返す

	bool CheckBomb(Vector3 dir_, ref float distance)
		引数方向上に爆弾があれば true を返し、distance にそこまでの距離を格納する
		爆弾がなければ false を返す

	bool CheckPlayer(Vector3 dir_, ref GameObject player)
		dir_の方向にプレイヤーがいれば true を返し、playerにそのプレイヤーを格納する
		プレイヤーがいなければ false を返す
		参考：取得した player が GetFriend() と同じかどうかで味方か敵かを識別できる
*/

// ======================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pawn : MonoBehaviour {

	public class Direction {
		public static Vector3 N = new Vector3(0f,0f,1f);
		public static Vector3 E = new Vector3(1f,0f,0f);
		public static Vector3 S = new Vector3(0f,0f,-1f);
		public static Vector3 W = new Vector3(-1f,0f,0f);
	}
	public readonly Vector3[] cardinalDirections = new Vector3[]{
		Direction.N,
		Direction.E,
		Direction.S,
		Direction.W
	};
	public const int cardinalDirectionNum = 4;
	
	Bomb bombPrefab;
	float moveSpeedMax = 2.5f;
	float moveSpeedRatio = 0f;
	Vector3 direction = new Vector3(1f,0f,0f);
	//Vector3 position;
	float downTime = 0.0f;
	const float downTimeValue = 3.5f;
	Rigidbody rb;
	Animator animator;
	NavMeshTool navi;
	public NavMeshTool GetNavMeshTool()
	{
		return navi;
	}

	int bombLevel = 1;
	int bodyTypeNo = 1;
	bool isDead = false;
	public bool IsDead()
	{
		return isDead;
	}
	TeamID teamId = TeamID.TeamZ;
	int myGroupNo;
	GameObject partner;
	
	
	public int GetBombLevel()
	{
		return bombLevel;
	}
	
	public TeamID GetTeamID()
	{
		return teamId;
	}
	
	// グループ番号を設定
	public void SetGroupNo(int no)
	{
		myGroupNo = no;
		
		// グループ番号にあわせてボディを変更
		if(no>0 && no<10){
			bodyTypeNo = no;
		}else{
			bodyTypeNo = 0;
		}
		string name = "Body_Group" + bodyTypeNo.ToString();
		Transform children = GetComponentInChildren < Transform > ();
		foreach(Transform t in children){
			if(t.name == name){
				t.gameObject.SetActive(true);
			}else{
				t.gameObject.SetActive(false);
			}
		}
	}

	// 設定されているグループ番号を取得
	public int GetGroupNo()
	{
		return myGroupNo;
	}
	
	public Vector3 GetPosition()
	{
		return transform.position;
	}
	
	public Vector3Int GetIntPosition()
	{
		Vector3 unitPosition = GetPosition();
		Vector3Int v = Vector3Int.RoundToInt(unitPosition);
		return v;
	}
	
	
	public Vector3 GetDirection()
	{
		return direction;
	}
	
	public void SetDirection(Vector3 dir)
	{
		dir.y = 0f;
		direction = dir.normalized;
/*		
		direction.x = Mathf.Round(direction.x);
		direction.z = Mathf.Round(direction.z);
		if(direction.magnitude < 0.5f){
			direction = new Vector3(0f,0f,1f);
		}
*/
	}
	
	public float GetMoveSpeed()
	{
		return moveSpeedMax * moveSpeedRatio;
	}
	
	public void SetMoveSpeedRatio(float speedRatio)
	{
		speedRatio = Mathf.Clamp(speedRatio, 0f, 1f);
		moveSpeedRatio = speedRatio;
	}
	
	// 壁までの距離を返す
	public float DistanceToWall(Vector3 dir_)
	{
		//Debug.DrawRay(position, direction, Color.white);
		//GameManager.instance.DispStr("Ray Dir:" + direction);

		RaycastHit hit;
		Ray ray = new Ray(GetPosition(), dir_);
		LayerMask layer = ~LayerMask.NameToLayer("Wall");
		
		Physics.Raycast(ray, out hit, Mathf.Infinity, layer);
		if(hit.collider){
			return hit.distance;
		}else{
			return float.PositiveInfinity;
		}
	}
	
	// dir_の方向に爆弾があれば true を返し、distance に爆弾までの距離を格納する
	// 爆弾がなければ false を返す
	public bool CheckBomb(Vector3 dir_, ref float distance)
	{
		Vector3 start = GetPosition();
		start.y = 0.5f;	// 地表ではない
		Ray ray = new Ray(start, dir_);
		RaycastHit hit;
		
		Physics.Raycast(ray, out hit);
		if(hit.collider != null && hit.collider.CompareTag("Bomb")){
			distance = hit.distance;
			return true;
		}
		
		distance = float.PositiveInfinity;
		return false;
	}
	
	// dir_の方向にプレイヤーがいれば true を返し、player にそのプレイヤーキャラクターのGameObjectを格納する
	// プレイヤーがいなければ false を返す
	public bool CheckPlayer(Vector3 dir_, ref GameObject player)
	{
		Vector3 start = GetPosition();
		//start += dir_.normalized * 0.5f;
		start.y = 0.5f;	// 地表ではない
		Ray ray = new Ray(start, dir_);
		RaycastHit hit;
		
		Physics.Raycast(ray, out hit);
		if(hit.collider != null && hit.collider.CompareTag("Player")){
			player = hit.collider.gameObject;
			return true;
		}
		
		player = null;
		return false;
	}
	
	// チームメイトの GameObject を取得する
	public GameObject GetFriend()
	{
		return partner;
	}
	
	public void ShootBomb()
	{
		if(downTime > 0f || isDead){
			return;
		}
		//爆弾はNSWE方向のみ
		Vector3 bomb_direction;
		bomb_direction.x = Mathf.Round(direction.x);
		bomb_direction.z = Mathf.Round(direction.z);
		bomb_direction.y = 0;
		
		Vector3 bombStartPosition = GetPosition() + bomb_direction * 0.8f;
		bombStartPosition.y = 0.5f;
		Bomb newBomb = Instantiate(bombPrefab, bombStartPosition, transform.rotation);
		newBomb.direction = bomb_direction;
		newBomb.level = bombLevel;
		animator.SetBool("running", false);
		downTime = downTimeValue;
		
		AudioSource audioSource = GetComponent<AudioSource>();
		PlayerSound clips = GetComponent<PlayerSound>();
		if(clips && audioSource)
			audioSource.PlayOneShot(clips.throwClip, 0.1f);
	}
	
	// =======================================================================
	void Awake()
	{
		rb = GetComponent<Rigidbody>();
		animator = GetComponent<Animator>();
		navi = new NavMeshTool(gameObject, GetComponent<UnityEngine.AI.NavMeshAgent>());

		bombPrefab = Resources.Load<Bomb>("Bomb");
		// NavMeshAgentによるGameObjectの動きを無効にする
		UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
		if(agent){
			agent.updatePosition = false;
			agent.updateRotation = false;
		}
		Transform parent = transform.parent;
		if(parent != null){
			// チームを取得
			Team tobj = parent.gameObject.GetComponent<Team>();
			teamId = tobj.teamId;
			
			List<GameObject> objList = new List<GameObject>();
			if(parent != null){
				for(int i=0;parent.childCount>i;i++){
					GameObject obj = parent.GetChild(i).gameObject;
					if(obj != gameObject){
						partner = obj;
						break;
					}
				}
			}
		}
		direction = transform.rotation * new Vector3(0f,0f,1f);
		isDead = false;
	}
	
	Vector3 m_EulerAngleVelocity = new Vector3(0, 240, 0);
	float rotationRatio = 3.0f;
	
	// Unity では Update() 前に実行される
	void FixedUpdate()
	{
		rb.linearVelocity = Vector3.zero;
		if(!isDead && downTime <= 0f){
			Vector3 vel = direction.normalized * moveSpeedMax * moveSpeedRatio;
			rb.MovePosition(transform.position + vel*Time.fixedDeltaTime);
			rb.linearVelocity = Vector3.zero;
/*
			Vector3 pos = transform.position+vel*Time.fixedDeltaTime;
			pos.y = 0f;
			transform.position = pos;
*/
		}
	}
	
	// Update() 後に実行
	void LateUpdate()
	{
		if(isDead){
			Quaternion deltaRotation = Quaternion.Euler(m_EulerAngleVelocity * rotationRatio * Time.deltaTime);
			transform.rotation = transform.rotation * deltaRotation;
			rotationRatio += 4.0f * Time.deltaTime;
			return;
		}
		// 向きの固定はする
		Vector3 target = this.transform.position + direction;
		transform.LookAt (target);
		
		if(downTime > 0f){
			downTime -= Time.deltaTime;
			animator.speed = 0.01f;
			return;
		}
		animator.speed = 1f;
		
		if(moveSpeedRatio > 0.01f){
			animator.SetBool("running", true);
		}else{
			animator.SetBool("running", false);
		}
		
		navi.UpdateCurrentPosition();		
	}
	
	public void Dead()
	{
		float delay = 1.5f;
		GetComponent<CapsuleCollider>().enabled = false;
		Destroy(gameObject, delay);
		isDead = true;
		AudioSource audioSource = GetComponent<AudioSource>();
		PlayerSound clips = GetComponent<PlayerSound>();
		if(clips)
			audioSource.PlayOneShot(clips.downClip, 0.5f);
	}
	
	public void BombLevelUp(GameObject item)
	{
		if(item==null){
			return;
		}
		if(item.CompareTag("Item")){
			item.transform.parent = transform;
			item.transform.localPosition = new Vector3(0f, 1f+0.5f*bombLevel, 0f);
			bombLevel+=1;
		}
	}
}
