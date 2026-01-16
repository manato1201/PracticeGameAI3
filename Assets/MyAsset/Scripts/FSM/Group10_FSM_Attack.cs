using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Group10
{

public class Group10_FSM_Attack : Group10_FSM_Base
{
	public Group10_FSM_Attack(ItemCollector s) : base(s)
	{
		
	}
	
	// このステートに遷移した最初に1度実行する
	public override void OnEnter()
	{
		// 向いている方向で、壁までの距離が爆風の長さより遠ければ
		if(pawn.DistanceToWall(pawn.GetDirection()) > (pawn.GetBombLevel()+1)){
			pawn.ShootBomb();
		}
	}
	
	// このステートで毎回実行する
	public override void OnUpdate()
	{
	}
	
	// このステートを終了する時に1度実行する
	public override void OnExit()
	{
	}
	
	// 遷移先のステートを返す
	// 今のステートを継続する時は、現在のステートを返す
	public override ItemCollector.State CheckTransitions()
	{
		// 元のステートに戻る：FREE_MOVEからしか遷移してこない
		return ItemCollector.State.FREE_MOVE;
	}
}

}
