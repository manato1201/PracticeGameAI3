// ブラックボード
// BlackBoardKey にメッセージの種類を追加しましょう
// 書けるメッセージには"int","float","Vector2"を作っていますが、余裕がある人は種類を増やしてみましょう
/* 使い方
    準備：項目名と値のセットでブラックボードに書きこむので、書き込み項目は事前に enum BlackBoardKey に追加しておく。
        書きこむことができる値は、int, float, Vector2
    
    １：ブラックボードを作る
    BlackBoard blackBoard;
    
    ２：ブラックボードに書きこむ
    BlackBoardKey.Move の値を書きこむ時
    int moveValue = 1;
    blackBoard.SetValue(BlackBoardKey.Move, moveValue);
        どの型で書きこむか混乱しないように、一旦変数に入れてから書きこむと良いでしょう。
    
    ３：ブラックボードに書かれている値を得る
    int moveValue; // 書かれている値を受け取る受け皿
    blackBoard.GetValue(BlackBoardKey.Move, moveValue);

    今までに値が設定されていない場合は、
        int は 0
        float は 0.0f
        Vector2 は Vector2(0.0f,0.0f)
    が返ります
    加えて false が返るので、未設定の時に上記の値を受け取ってまずい場合は、返り値をチェックしてください。
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BlackBoardKey {
    Move,
    Target,
    Time,
}

public class BlackBoard
{
    Dictionary<BlackBoardKey, int> IntValues = new Dictionary<BlackBoardKey, int>();
    Dictionary<BlackBoardKey, float> FloatValues = new Dictionary<BlackBoardKey, float>();
    Dictionary<BlackBoardKey, Vector2> Vector2Values = new Dictionary<BlackBoardKey, Vector2>();
    
    // key に value の値をセットする
    public void SetValue(BlackBoardKey key, int value){
        IntValues[key] = value;
    }
    // key の値を取得して、value に格納する
    // 返り値：key の値がない場合は false が返る
    public bool GetValue(BlackBoardKey key, out int value){
        if (IntValues.ContainsKey(key))
        {
            value = IntValues[key];
            return true;
        }
        value = 0;
        return false;
    }

    // key に value の値をセットする
    public void SetValue(BlackBoardKey key, float value){
        FloatValues[key] = value;
    }
    // key の値を取得して、value に格納する
    // 返り値：key の値がない場合は false が返る
    public bool GetValue(BlackBoardKey key, out float value){
        if (FloatValues.ContainsKey(key))
        {
            value = FloatValues[key];
            return true;
        }
        value = 0f;
        return false;
    }

    // key に value の値をセットする
    public void SetValue(BlackBoardKey key, Vector2 value){
        Vector2Values[key] = value;
    }
    // key の値を取得して、value に格納する
    // 返り値：key の値がない場合は false が返る
    public bool GetValue(BlackBoardKey key, out Vector2 value){
        if (Vector2Values.ContainsKey(key))
        {
            value = Vector2Values[key];
            return true;
        }
        value = new Vector2(0f,0f);
        return false;
    }
    
}
