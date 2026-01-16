/*
*/

using UnityEngine;
namespace Group06
{

    public class Group06Player : Pawn
    {
        int groupNo = 6;
        NavMeshTool navi;
        // ----------------------------------------
        enum State
        {
            WAIT,
        };
        State state = 0;
        // ステートを nextStateにする
        void StateChange(State nextState)
        {
            state = nextState;
        }
        // ----------------------------------------


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
            StateChange(State.WAIT);
        }

    }

}   // Group06