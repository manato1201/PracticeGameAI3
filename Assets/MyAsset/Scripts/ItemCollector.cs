using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Group10
{

public class ItemCollector : Pawn
{
	public int groupNo = 0;
	public NavMeshTool navi;
	// ----------------------------------------
	public enum State {
		SELECT_ITEM,
		MOVE_TO_ITEM,	// アイテムに向かって移動
		FREE_MOVE,		// 適当な移動
		ATTACK,
	};
	State currentState;
	
	public int targetItemNo = -1;

	private List<Group10_FSM_Base> availableStates = new List<Group10_FSM_Base>();

	// Start is called before the first frame update
	void Start()
	{
		// 経路探索機能を取得します
		navi = GetNavMeshTool();
		SetGroupNo(groupNo);
		
		availableStates.Add(new Group10_FSM_SelectItem(this));
		availableStates.Add(new Group10_FSM_MoveToItem(this));
		availableStates.Add(new Group10_FSM_FreeMove(this));
		availableStates.Add(new Group10_FSM_Attack(this));

		currentState = State.SELECT_ITEM;
		availableStates[(int)currentState].OnEnter();
	}

	// Update is called once per frame
	void Update()
	{
		State nextState = availableStates[(int)currentState].CheckTransitions();
		if (nextState != currentState)
		{
			//今のステートを終了
			availableStates[(int)currentState].OnExit();
			//次のステートの前処理
			availableStates[(int)nextState].OnEnter();
			//新しいステートを記録
			currentState = nextState;
		}
		//ステート実行
		availableStates[(int)currentState].OnUpdate();
	}
}

} // Group10
