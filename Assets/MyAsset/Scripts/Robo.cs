using UnityEngine;

public class Robo : Pawn
{
	public int set = 0;
	
	Vector3[] targetPositions;
	bool[] hitStatus;
	int nextTarget;
	int numTargets;
	UnityEngine.AI.NavMeshPath path;
	
	int step = 0;
	
	
	// Start is called before the first frame update
	void Start()
	{
		numTargets = GameManager.instance.NumTargets();
		targetPositions = new Vector3[numTargets];
		hitStatus = new bool[numTargets];
		for(int i=0; i<numTargets; i++){
			targetPositions[i] = GameManager.instance.TargetPosition(i);
			hitStatus[i] = false;
		}
		path = new UnityEngine.AI.NavMeshPath();
		nextTarget = 0;
		
		UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
		agent.updatePosition = false;
		agent.updateRotation = false;
	}

	// Update is called once per frame
	void Update()
	{
		UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
		//GameManager.instance.DispStr("ToWall:" + DistanceToWall());
		//GameManager.instance.DispStr("ToBomb:" + DistanceToBomb());
		//GameManager.instance.DispStr("Next:" + nextTarget);
		
		switch(step){
			case 0:
				SetMoveSpeed(0f);
				if(set == 0){
					for(int i=0; i<numTargets; i++){
						if(GameManager.instance.IsTargetActive(i)){
							nextTarget = i;
							break;
						}
					}
				}else{
					for(int i=numTargets-1; i>0; i--){
						if(GameManager.instance.IsTargetActive(i)){
							nextTarget = i;
							break;
						}
					}
				}
				
				agent.destination = targetPositions[nextTarget];
				UnityEngine.AI.NavMesh.CalculatePath(GetPosition(), agent.destination, UnityEngine.AI.NavMesh.AllAreas, path);
				step++;
				break;
			case 1:
				if(agent.pathPending){
					break;
				}
				step++;
				break;
			case 2:
				if(agent.pathPending){
					Debug.Log("pathPending");
				}
				for(int i=0; i<path.corners.Length-1; i++){
					Vector3 p0 = path.corners[i];
					Vector3 p1 = path.corners[i+1];
					Debug.DrawLine(p0, p1, Color.red);
					//GameManager.instance.DispStr("Path:" + path.corners[i]);
				}
				{
					Vector3 nextPos = agent.nextPosition;
					Vector3 targetPos = agent.steeringTarget;
					Vector3 delta = targetPos - GetPosition();
					GameManager.instance.DispStr("Next:" + targetPos);
					Debug.DrawLine(nextPos, GetPosition(), Color.green);
					GameManager.instance.DispStr("toNext:" + delta.magnitude);
					SetDirection(delta);
					SetMoveSpeed(5f);
					agent.nextPosition = GetPosition();
					if(agent.remainingDistance < agent.stoppingDistance){
						step = 0;
					}
					float r = 0;
					if(FrontCheckAlly(ref r) && r < 3f){
						GameManager.instance.DispStr("toAlly:" + r);
						Vector3 dir = GetDirection();
						dir = Quaternion.Euler( 0, 30, 0 ) * dir;
						SetDirection(dir);
					}
					// agent.remainingDistance 
					// pathPending が true の時は０
					// 少し遠いと infinity になりがち
				}
				break;
		}
	}
	
	public override void TargetHit(int index)
	{
		hitStatus[index] = true;
		for(int i=0; i<numTargets; i++){
			if(!hitStatus[i]){
				nextTarget = i;
				step = 0;
				break;
			}
		}
	}
	
}

