/*
	今回の課題用：

	public int NumTargets()
		ターゲットの数を返します
		ターゲットは、０番から NumTarget()-1 番までの番号を持ちます

	public Vector3 TargetPosition(int index)
		index番のターゲットの位置を返します

	public bool IsTargetActive(int index)
		index番のターゲットが残っていたら true を返します
		消えていたら false を返します

	その他：
	public static GameManager instance;
		インスタンスを取得します
	
	public void DispStr(string str)
		str を画面に表示します
		呼び出した順に上から表示します

*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Text;
using TMPro;

public class GameManager : MonoBehaviour
{
	public static GameManager instance;
	public Text text;
	
	// デバッグ用文字表示 DispStr用
	GameObject scoreText;
	StringBuilder buffer = new StringBuilder();
	
	int numTargets = 0;
	Vector3[] targetPositions;
	bool[] isTargetAvailable;
	float startTime;
	float clearTime;
	int targetsLeft;

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

	public GroupID groupNo = GroupID.Group0;
	

	void Awake()
	{
		instance = this;
		
		// 目標の管理
		GameObject[] targets = GameObject.FindGameObjectsWithTag("Bomb");
		numTargets = targets.Length;
		targetsLeft = numTargets;

		targetPositions = new Vector3[numTargets];
		isTargetAvailable = new bool[numTargets];
		for(int i=0; i<numTargets; i++){
			Target target = targets[i].GetComponent<Target>();		
			targetPositions[i] = target.transform.position;
			isTargetAvailable[i] = true;
			target.index = i;
		}
		
		GameObject team, chara_1, chara_2;
		team = GameObject.Find("Team");
		chara_1 = GameObject.Find("Chara_1");
		chara_2 = GameObject.Find("Chara_2");
		CharaSetup(groupNo, team, chara_1, chara_2);
	}
	
	public int NumTargets()
	{
		return numTargets;
	}
	
	public Vector3 TargetPosition(int index)
	{
		if(index < 0 || index >= numTargets){
			return Vector3.zero;
		}
		return targetPositions[index];
	}
	
	public bool IsTargetActive(int index)
	{
		if(index < 0 || index >= numTargets){
			return false;
		}
		return isTargetAvailable[index];
	
	}

	
	// Start is called before the first frame update
	void Start()
	{
		scoreText = GameObject.Find("DebugText");
		text.text = "----";
		startTime = Time.time;
	}

	// Update is called once per frame
	void Update()
	{
		if(targetsLeft <= 0){
			float delta = clearTime - startTime;
			text.text = "Finish:" + delta.ToString("N2") + "sec";
		}else{
			text.text = "Left:" + targetsLeft;
		}
	}
	
	public void decTarget(int index)
	{
		isTargetAvailable[index] = false;
		targetsLeft -= 1;
		if(targetsLeft == 0){
			clearTime = Time.time;
		}
	}
	
	void LateUpdate()
	{
		if(scoreText != null){
			scoreText.GetComponent<TextMeshProUGUI>().text = buffer.ToString();
		}
		buffer.Clear();
	}
	
	public void DispStr(string str){
		buffer.Append(str + "\n");
	}
	
	void CharaSetup(GroupID group, GameObject team, GameObject player1,GameObject player2){
		switch(group){
			case GroupID.Group1:
			player1.AddComponent<Group01.Group01Player>();
			break;
			case GroupID.Group2:
			player1.AddComponent<Group02.Group02Player>();
			break;
			case GroupID.Group3:
			player1.AddComponent<Group03.Group03Player>();
			break;
			case GroupID.Group4:
			player1.AddComponent<Group04.Group04Player>();
			break;
			case GroupID.Group5:
			player1.AddComponent<Group05.Group05Player>();
			break;
			case GroupID.Group6:
			player1.AddComponent<Group06.Group06Player>();
			break;
			case GroupID.Group7:
			player1.AddComponent<Group07.Group07Player>();
			break;
			case GroupID.Group8:
			player1.AddComponent<Group08.Group08Player>();
			break;
			case GroupID.Group9:
			player1.AddComponent<Group09.Group09Player>();
			break;
			case GroupID.Group0:
			player1.AddComponent<Robo>();
			player2.AddComponent<Robo>();
			break;
			default:
			// 何もしない
			break;
		}
	}


}
