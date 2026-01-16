// トークン管理
/*	使い方
	１：トークンソースを作る
	TokenSource tokenSource = new TokenSource(5);
	
	トークンの数は後から返ることができる
	tokenSource.SetMaxNumber(10)
		数を減らした場合は、発行済のトークンが返却されて新しく設定した上限数未満になるまで、トークンを発行できない
	
	２：トークンを発行する
	Token token = tokenSource.GetToken();
	発行済トークン数が上限に達している場合、 null が返る
	if(token != null){} みたいな感じで、Token が取れたら、という処理をする
	
	３：トークンを管理する
	発行したトークンは保持しておく（どこかに保管しておく）
	トークンの数分 GameObject を作る、なら、その GameObject に持たせておく
		gameObject.token = token; とか
		
		ちなみに、Token はデストラクタで自分を返却するようにしているので、
		その GameObject の token に既にトークンが設定されていても勝手に返却されて事故らないはず。
	
	４：使い終わったトークンを返却する
	token.Return() を実行すると、発行元の TokenSource にトークンが返却される
		同じ Tokenで token.Return() を複数回実行しても返却処理は1回しか実行されない
		返却したら権利はないことになるので、そのつもりで処理を作る。
*/
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// --------------------------------------------------
// トークンソース（プール）
public class TokenSource {
	// トークンを溜める場所を作る
	// numTokens : トークンの数（SetMaxNumber()で後から変更できる）
	public TokenSource(uint numTokens)
	{
		maxToken = numTokens;
	}
	// トークンの最大数を設定する
	public void SetMaxNumber(uint n)
	{
		maxToken = n;
	}
	// トークンを取得する
	// 取得できない場合は null が返る
	// 取得したトークンは使い終わったら、Token.Return を実行すること
	public Token GetToken()
	{
		if(GetRemain() > 0){
			Token newToken = new Token(this);
			DecreaseToken();
			return newToken;
		}
		return null;
	}

	// 以下は仕組み----------------------------------
	void DecreaseToken()
	{
		numUsed += 1;
	}
	uint GetRemain()
	{
		if(maxToken > numUsed){
			return maxToken - numUsed;
		}else{
			return 0;
		}
	}
	
	public void ReturnToken(Token token)
	{
		// public にせざるを得ないので、問題が発生しないように厳重にチェックします
		// 本当は Token からしか実行できないようにしたい。
		if(token.GetSource() == this){
			if(numUsed > 0){
				numUsed -= 1;
			}else{
				Debug.LogError("Token number contradiction");
			}
		}else{
			Debug.LogError("Token mismatch");
		}
	}
	uint numUsed = 0;
	uint maxToken = 0;
}

// --------------------------------------------------
// トークン
public class Token {
	// トークンを返す
	public void Return()
	{
		if(origin != null){
			origin.ReturnToken(this);
		}
		origin = null;
	}
	
	// 以下は仕組み -------------------------------
	public Token(TokenSource source)
	{
		origin = source;
	}
	// トークンの出所を得る
	public TokenSource GetSource()
	{
		return origin;
	}
	// デストラクタで自身を返却するようにする（返却忘れ防止）
	~Token()
	{
		Return();
	}
		
	
	TokenSource origin;
}
