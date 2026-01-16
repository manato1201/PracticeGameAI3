/*
	今回の課題用の関数：
	public int NumItems()
		フィールド上のアイテムの数を返します
		アイテムは、０番から NumItems()-1 番までの番号を持ちます

	public Vector3 ItemPosition(int index)
		index番のアイテムの位置を返します

	public bool IsItemAvailable(int index)
		index番のアイテムが残っていたら true を返します
		消えていたら false を返します

	public bool IsPlayerDead(TeamID team, int playerIndex)
		team の teamIndex 番目のプレイヤーが死んでいたら true を返します
		対象キャラクターがいない場合も true を返します

	public Vector3 GetPlayerPosition(TeamID team, int playerIndex)
		team の teamIndex 番目のプレイヤーの位置を返します
		ただし、対象キャラクターがいない場合は Vector3.zero を返します

	その他：
	public static GameManager instance;
		インスタンスを取得します

	public void DispStr(string str)
		str を画面に表示します
		呼び出した順に上から表示します
*/

// ======================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Text;
using UnityEngine.SceneManagement;
using TMPro;

public enum TeamID {
	TeamZ = 0,
	TeamA = 1,
	TeamB = 2,
}

public class GameManager : MonoBehaviour
{
	public static GameManager instance;
	public Text text;
	public AudioClip startClip;
	public AudioClip endClip;
	public AudioClip warnClip;

	AudioSource audioSource;
	float volume;

	public enum GroupID {
		Group0,
		Group1,
		Group2,
		Group3,
		Group4,
		Group5,
		Group6,
		Group7,
		Group8,
		Group9,
		Group10,
	}

	public GroupID teamA_Group = GroupID.Group0;
	public GroupID teamB_Group = GroupID.Group0;

	// デバッグ用文字表示 DispStr用
	GameObject debugText;
	StringBuilder buffer = new StringBuilder();

	Item[] items;
	int numItems;
	float startTime;
	float EndTime;
	bool playing;
	Bomb bombPrefab;

	// 停止チェック用
	const int numPlayers = 4;
	Pawn[] playerPawns = new Pawn[numPlayers];
	float[] stopTimer = new float[numPlayers];
	Vector3[] stopCheckPos = new Vector3[numPlayers];
	const float maxStopping = 10f; // 停止許可時間

	void Awake()
	{
		instance = this;
		debugText = GameObject.Find("DebugText");

		// アイテムのリスト作成
		GameObject[] itemObjects = GameObject.FindGameObjectsWithTag("Item");
		numItems = itemObjects.Length;
		items = new Item[numItems];
		for(int i=0; i<numItems; i++){
			Item item = itemObjects[i].GetComponent<Item>();
			items[i] = item;
		}

		// タイムアップボム用
		bombPrefab = Resources.Load<Bomb>("Bomb");

		GameObject teamAObject, player_a1, player_a2;
		GameObject teamBObject, player_b1, player_b2;
		teamAObject = GameObject.Find("Team_A");
		player_a1 = GameObject.Find("Player_a1");
		player_a2 = GameObject.Find("Player_a2");
		teamBObject = GameObject.Find("Team_B");
		player_b1 = GameObject.Find("Player_b1");
		player_b2 = GameObject.Find("Player_b2");
		// Awake で他 GameObject を参照している場合は、次の順番で行う
		// team.SetActive(false) ... Find() で null が返るので上のように先に取得しておく
		// スクリプトをアタッチ、そのまま enabled = false
		// スクリプトの enabled = false のまま、team.SetActive(true)
		// 各スクリプトの enabled = true
		GroupSetup(teamA_Group, teamAObject, player_a1, player_a2);
		GroupSetup(teamB_Group, teamBObject, player_b1, player_b2);

		// 停止チェック用
		for(int i=0; i<numPlayers; i++){
			stopTimer[i] = 0f;
		}
	}

	public int NumItems()
	{
		return items.Length;
	}

	public Vector3 ItemPosition(int index){
		if(index < 0 || index >= numItems){
			return Vector3.zero;
		}
		return items[index].transform.position;
	}
	public bool IsItemAvailable(int index)
	{
		if(index < 0 || index >= numItems){
			return false;
		}
		if(items[index].taken){
			return false;
		}else{
			return true;
		}

	}

	const int numTeamPlayers = 2;
	class TeamStatus {
		bool[] alive = new bool[numTeamPlayers];
		public Pawn[] members = new Pawn[numTeamPlayers];
		public void SetMember(int idx, Pawn p)
		{
			if(idx < 0 || idx >= numTeamPlayers)
				return;
			alive[idx] = true;
			members[idx] = p;
		}
		public void UpdateTeamStatus()
		{
			for(int i=0;i<numTeamPlayers;i++){
				Pawn p = members[i];
				if(!p || p.IsDead()){
					alive[i] = false;
				}
			}
		}
		public bool isTeamLost()
		{
			bool lose = true;
			for(int i=0; i<numTeamPlayers; i++){
				if(alive[i]) lose = false;
			}
			return lose;
		}
	}
	TeamStatus teamA = new TeamStatus();
	TeamStatus teamB = new TeamStatus();

	TeamID winner = TeamID.TeamZ;

	public Vector3 GetPlayerPosition(TeamID team, int playerIndex)
	{
		if(playerIndex < 0 || playerIndex >= 2){
			return Vector3.zero;
		}
		Pawn p = null;
		if(team == TeamID.TeamA){
			p = teamA.members[playerIndex];
		}
		if(team == TeamID.TeamB){
			p = teamB.members[playerIndex];
		}
		if(!p){
			return Vector3.zero;
		}
		return p.GetPosition();
	}

	public bool IsPlayerDead(TeamID team, int playerIndex)
	{
		if(playerIndex < 0 || playerIndex >= 2){
			return true;
		}
		Pawn p = null;
		if(team == TeamID.TeamA){
			p = teamA.members[playerIndex];
		}
		if(team == TeamID.TeamB){
			p = teamB.members[playerIndex];
		}
		if(!p){
			return true;
		}
		return p.IsDead();
	}

	// Start is called before the first frame update
	void Start()
	{
		text.text = "----";
		startTime = Time.time;

		GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
		int teamA_idx = 0;
		int teamB_idx = 0;
		for(int i=0; i<players.Length; i++){
			Pawn player = players[i].GetComponent<Pawn>();
			if(player){
				playerPawns[i] = player;
				if(player.GetTeamID() == TeamID.TeamA){
					teamA.SetMember(teamA_idx, player);
					teamA_idx++;
				}
				if(player.GetTeamID() == TeamID.TeamB){
					teamB.SetMember(teamB_idx, player);
					teamB_idx++;
				}
			}
		}
		audioSource = GetComponent<AudioSource>();
		audioSource.PlayDelayed(2.0f);
		audioSource.PlayOneShot(startClip, 1.0f);
		volume = audioSource.volume;
		playing = true;
		StartCoroutine (GameWait(0f, 1f));
	}

	float timeUpTimer = 0f;

	// Update is called once per frame
	void Update()
	{
		// 勝者チェック
		if(playing){
			float pastTime = Time.time - startTime;
			text.text = "Time:" + pastTime.ToString("N2") + "sec";
			teamA.UpdateTeamStatus();
			teamB.UpdateTeamStatus();
			bool teamA_lost = teamA.isTeamLost();
			bool teamB_lost = teamB.isTeamLost();
			if(teamA_lost)
				winner = TeamID.TeamB;
			if(teamB_lost)
				winner = TeamID.TeamA;
			if(teamA_lost && teamB_lost)
				winner = TeamID.TeamZ;
			if(teamA_lost || teamB_lost){
				audioSource.PlayOneShot(endClip, 1.0f);
				EndTime = Time.time;
				playing = false;
				StartCoroutine (GameWait(0.1f, 0.5f));
				StartCoroutine (Fadeout ());
			}

		}else{
			// 結果表示
			float pastTime = EndTime - startTime;
			string timeText = " Time:" + pastTime.ToString("N2") + "sec";
			if(winner == TeamID.TeamA){
				text.text = " TeamA win!" + timeText;
			}
			else if(winner == TeamID.TeamB){
				text.text = " TeamB win!" + timeText;
			}else{
				text.text = " Draw";
			}

			if(Input.GetKey(KeyCode.Space)){
				SceneManager.LoadScene("GameField");
			}
		}

		// 一定時間止まっているキャラクターへの罰
		if(playing){
			for(int i=0; i<numPlayers; i++){
				if(CheckStop(i)){
					StopPunishBomb(playerPawns[i]);
				}
			}
		}

		// timeUp以上の時間がかかったら、爆弾が投げ込まれる
		if(playing){
			float pastTime = Time.time - startTime;
			float timeUp = 60f;
			if(pastTime > timeUp){
				int[] points = {0,1,2,3,4,5};
				Shuffle(points);
				timeUpTimer -= Time.deltaTime;
				if(timeUpTimer < 0f){
					audioSource.PlayOneShot(warnClip, 2.0f);
					int n = Mathf.FloorToInt((pastTime - timeUp)/5f) + 1;
					if(n > 6)
						n = 6;
					for(int i=0; i<n; i++){
						TimeUpBomb(points[i]);
					}
					timeUpTimer = 5f;
				}
			}
		}

	}
    void Shuffle (int[] deck) {
        for (int i = 0; i < deck.Length; i++) {
            int temp = deck[i];
            int randomIndex = Random.Range(0, deck.Length);
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }
    }

	static readonly float[] axisPoints = {1,4,7,9,12,15};

	void TimeUpBomb(int startPoint)
	{
		int xorz = Random.Range(0,2);
		int uord = Random.Range(0,2);
		int n = startPoint;
		// 開始位置
		Vector3 shootPoint = Vector3.zero;
		shootPoint.y = 0.5f;
		Vector3 direction;
		if(xorz == 0){
			shootPoint.x = axisPoints[n];
			if(uord == 0){
				direction = Bomb.Direction.N;
				shootPoint.z = 1;
			}else{
				direction = Bomb.Direction.S;
				shootPoint.z = 15;
			}
		}else{
			shootPoint.z = axisPoints[n];
			if(uord == 0){
				direction = Bomb.Direction.E;
				shootPoint.x = 1;
			}else{
				direction = Bomb.Direction.W;
				shootPoint.x = 15;
			}
		}
		Bomb newBomb = Instantiate(bombPrefab, shootPoint, Quaternion.identity);
		newBomb.direction = direction;
		newBomb.level = 3;
	}

	// 長時間停止違反で true が返る
	bool CheckStop(int playerIndex)
	{
		Pawn p = playerPawns[playerIndex];
		if(!p || p.IsDead()){
			return false;
		}
		Vector3 last = stopCheckPos[playerIndex];
		Vector3 d = p.transform.position - last;
		if(d.sqrMagnitude < 0.1f){
			stopTimer[playerIndex] += Time.deltaTime;
		}else{
			stopTimer[playerIndex] = 0f;
			stopCheckPos[playerIndex] = p.transform.position;
		}

		if(stopTimer[playerIndex] > maxStopping){
			// 一回チェックにひっかかったら、またしばらく猶予を与える
			stopTimer[playerIndex] = 0f;
			return true;
		}else{
			return false;
		}
	}

	// 停止違反対応爆弾
	void StopPunishBomb(Pawn p)
	{
		if(!p || p.IsDead()){
			return;
		}
		// 空いてる方向を探す
        float[] distances = new float[Pawn.cardinalDirectionNum];
		int[] indices = new int[Pawn.cardinalDirectionNum];
        // 4方向の壁までの距離をチェック
        for(int i=0; i<Pawn.cardinalDirectionNum; i++){
            distances[i] = p.DistanceToWall(p.cardinalDirections[i]);
			indices[i] = i;
        }
		// スペースがある順番を作成
		System.Array.Sort(indices, (a,b) => distances[b].CompareTo(distances[a]));

		// 壁までの距離が長い順に処理
		int dir = indices[0];
		for(int i=0; i<Pawn.cardinalDirectionNum; i++){
			dir = indices[i];
			if(distances[dir] < 3f){
				break;
			}
			// 爆弾のセット候補位置
			Vector3 offset = p.cardinalDirections[dir];
			Vector3 shootPoint = p.GetIntPosition() + offset*3f;
			// 他のキャラが近くにいたらスキップ
			if(NearPlayerNum(shootPoint) < 3){
				// dir が爆弾セット方向
				Bomb newBomb = Instantiate(bombPrefab, shootPoint, Quaternion.identity);
				newBomb.direction = -offset;
				newBomb.level = 1;
				break;
			}
		}

	}

	// pos の位置から半径d以内にいるプレイヤー数を返す
	int NearPlayerNum(Vector3 pos, float d=3f)
	{
		int n = 0;
		d = d*d;
		for(int i=0; i<numPlayers; i++){
			Pawn p = playerPawns[i];
			if(!p || p.IsDead()){
				continue;
			}
			Vector3 f = pos - playerPawns[i].transform.position;
			if(f.sqrMagnitude < d){
				n += 1;
			}
		}
		return n;
	}


	IEnumerator Fadeout ()
	{
		yield return new WaitForSeconds(3f);
		for (int i=0; i<100; i++) {
			audioSource.volume =  volume * 0.8f;
			volume = audioSource.volume;
			yield return new WaitForSeconds(0.1f);
		}
		audioSource.volume = 0;
		audioSource.enabled = false;
	}

	IEnumerator GameWait(float preWait, float waitTime)
	{
		while(preWait > 0f){
			preWait -= Time.deltaTime;
			yield return null;
		}
		Time.timeScale = 0f;
		yield return new WaitForSecondsRealtime(waitTime);
		Time.timeScale = 1f;
	}

	void LateUpdate()
	{
		if(debugText != null){
			debugText.GetComponent<TextMeshProUGUI>().text = buffer.ToString();
		}
		buffer.Clear();
	}

	public void DispStr(string str){
		buffer.Append(str + "\n");
	}

	// ---------------------------------
	void GroupSetup(GroupID group, GameObject team, GameObject player1,GameObject player2){
		switch(group){
			case GroupID.Group1:
			// team.AddComponent<Group01.Group01Team>();
			// player1.AddComponent<Group01.Group01Player>();
			//player2.AddComponent<Group01.Group01Player>();
			break;
			case GroupID.Group2:
			// team.AddComponent<Group02.Group02Team>();
			// player1.AddComponent<Group02.Group02Player>();
			//player2.AddComponent<Group02.Group02Player>();
			break;
			case GroupID.Group3:
			team.AddComponent<Group03.Group03Team>();
			player1.AddComponent<Group03.Group03Player>();
			//player2.AddComponent<Group03.Group03Player>();
			break;
			case GroupID.Group4:
			team.AddComponent<Group04.Group04Team>();
			player1.AddComponent<Group04.Group04Player>();
			//player2.AddComponent<Group04.Group04Player>();
			break;
			case GroupID.Group5:
			team.AddComponent<Group05.Group05Team>();
			player1.AddComponent<Group05.Group05Player>();
			player2.AddComponent<Group05.Group05Player>();
			break;
			case GroupID.Group6:
			team.AddComponent<Group06.Group06Team>();
			player1.AddComponent<Group06.Group06Player>();
			player2.AddComponent<Group06.Group06Player>();
			break;
			case GroupID.Group7:
			// team.AddComponent<Group07.Group07Team>();
			// player1.AddComponent<Group07.Group07Player>();
			// player2.AddComponent<Group07.Group07Player>();
			break;
			case GroupID.Group8:
			// team.AddComponent<Group08.Group08Team>();
			// player1.AddComponent<Group08.Group08Player>();
			// //player2.AddComponent<Group08.Group08Player>();
			break;
			case GroupID.Group9:
			// team.AddComponent<Group09.Group09Team>();
			// player1.AddComponent<Group09.Group09Player>();
			//player2.AddComponent<Group09.Group09Player>();
			break;
			case GroupID.Group10:
			player1.AddComponent<RandomWalk>();
			player2.AddComponent<RandomWalk>();
			break;
			case GroupID.Group0:
			// 何もしない
			break;
			default:
			break;
		}
		// 基本的な処理 (Pawn) は必ずつける
		if(player1.GetComponent<Pawn>()==null){
			player1.AddComponent<JustStand>();
		}
		if(player2.GetComponent<Pawn>()==null){
			player2.AddComponent<JustStand>();
		}
	}
}
