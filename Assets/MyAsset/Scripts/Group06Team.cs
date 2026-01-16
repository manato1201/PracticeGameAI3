using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Group06 {
	
public class Group06Team : MonoBehaviour
{
	Pawn[] teamMember;
	
	// Start is called before the first frame update
	void Start()
	{
		// チームメンバーを取得
		teamMember = new Pawn[transform.childCount];
		for(int i=0;transform.childCount>i;i++){
			GameObject obj = transform.GetChild(i).gameObject;
			teamMember[i] = obj.GetComponent<Pawn>();
		}
		
	}

	// Update is called once per frame
	void Update()
	{
		
	}
}

}
