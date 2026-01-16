/*
    何もせず、立っているだけのプレイヤー
*/
using UnityEngine;

public class JustStand : Pawn
{
    int groupNo = 0;
    
    // Start is called before the first frame update
    void Start()
    {
        SetGroupNo(groupNo);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
