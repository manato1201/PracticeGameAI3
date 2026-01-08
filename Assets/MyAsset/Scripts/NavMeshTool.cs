/*
	UnityのNavMeshを使うためのクラスです

	public NavMeshTool(GameObject me_, UnityEngine.AI.NavMeshAgent agent_)
		動かすGameObjectとNavMeshAgentを登録したインスタンスを作ります
	
	public void SetDestination(Vector3 goal)
		goal に向かう経路を作成します。
		IsReady() が true を返すまで、経路データは作成されていません

	public void UpdateCurrentPosition()
		SetDestination() で作成した経路を移動している GameObject の位置を更新します。
		随時更新する（この関数を呼び出す）ことで、MoveDirection() が正しい値を返します。
	
	public Vector3 MoveDirection()
		SetDestination() で設定した目的地へ向かう方向を返します。
	
	public bool IsArrived()
		SetDestination() で設定した目的地に到着していたら true が返ります
	
	public Vector3 NextTargetPosition()
		SetDestination()で設定した経路上で、現在向かっているポイント（経路途中の点、または目的地）を返します
	
	おまけ
	public static float CalculatePathLength(Vector3 start, Vector3 end)
	現在のナビメッシュデータを使って、 start から end までの経路の距離を取得します。
	start と end をつなぐ経路がない場合は無限大を返します。
	注：重い処理になります
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NavMeshTool {

	UnityEngine.AI.NavMeshAgent agent;
	GameObject me;

	public static float CalculatePathLength(Vector3 start, Vector3 end)
	{
		UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
		float total = 0f;
		if(UnityEngine.AI.NavMesh.CalculatePath(start, end, UnityEngine.AI.NavMesh.AllAreas, path)){
			for(int i=0; i<path.corners.Length-1; i++){
				Vector3 p0 = path.corners[i];
				Vector3 p1 = path.corners[i+1];
				Vector3 d = p0 - p1;
				total += d.magnitude;
			}
			return total;
		}else{
			return float.PositiveInfinity;
		}
	}	
	
	public NavMeshTool(GameObject me_, UnityEngine.AI.NavMeshAgent agent_)
	{
		me = me_;
		
		// NavMeshAgent によるGameObjectの操作を無効にします
		// (NavMeshAgent が動かすときは RigidBody の isKinetic をチェックを入れます)
		// Auto Braking のチェックも外します (使わない)
		agent = agent_;
		agent.updatePosition = false;
		agent.updateRotation = false;
	}
	
	public void SetDestination(Vector3 goal)
	{
		agent.destination = goal;
	}
	
	public bool IsReady()
	{
		return !agent.pathPending;
	}
	
	public Vector3 MoveDirection()
	{
		Vector3 direction = NextTargetPosition() - me.transform.position;
		return direction.normalized;
	}
	
	public void UpdateCurrentPosition()
	{
		agent.nextPosition = me.transform.position;
	}
	
	public bool IsArrived()
	{
		if(agent.remainingDistance < 0.3f)
			return true;
		else
			return false;
	}

	public Vector3 NextTargetPosition()
	{
		return agent.steeringTarget;
	}
	
}
