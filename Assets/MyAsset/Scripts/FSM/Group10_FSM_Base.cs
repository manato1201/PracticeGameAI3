using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Group10
{

/*	ステートマシンの各ステートの基底クラス
	継承して、
	- コンストラクタ
	- OnEnter()	(必要な場合のみ)
	- OnUpdate()
	- OnExit() 	(必要な場合のみ)
	- CheckTransition() 
	を実装する。
*/
public class Group10_FSM_Base
{
	protected ItemCollector pawn;
	
	public Group10_FSM_Base(ItemCollector s)
	{
		pawn = s;
	}
	
	// このステートに遷移した最初に1度実行する
	public virtual void OnEnter()
	{
		
	}
	
	// このステートで毎回実行する
	public virtual void OnUpdate()
	{
		
	}
	
	// このステートを終了する時に1度実行する
	public virtual void OnExit()
	{
		
	}
	
	// 遷移先のステートを返す
	// 今のステートを継続する時は、現在のステートを返す
	public virtual ItemCollector.State CheckTransitions()
	{
		return ItemCollector.State.SELECT_ITEM;
	}
	
}

}
