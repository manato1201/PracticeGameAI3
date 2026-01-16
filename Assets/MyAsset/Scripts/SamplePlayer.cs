using UnityEngine;

namespace sample {

public class SamplePlayer : Pawn
{
	NavMeshTool navi;
	int step = 0;
	int groupNo = 1;
	
	// Start is called before the first frame update
	void Start()
	{
		// 経路探索機能を取得します
		navi = GetNavMeshTool();
		SetGroupNo(groupNo);
	}

	// Update is called once per frame
	void Update()
	{
		switch(step){
			case 0:
				SetMoveSpeedRatio(0f);
				navi.SetDestination(GameManager.instance.ItemPosition(0));
				step++;
			break;
			case 1:
				if(navi.IsReady()){
					// 作成された経路に従って移動します
					SetDirection(navi.MoveDirection());
					SetMoveSpeedRatio(1f);
					// 目的地についたら、次の行動を設定します
					if(navi.IsArrived()){
						step++;
					}
				}
			break;
			case 2:
				SetDirection(new Vector3(0f,0f,1f));
				ShootBomb();
			break;
		}
	}
}

}
