using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ScreenPocket.Editor
{
    /// <summary>
    /// AnimationClipをダブルクリックしたら別窓で再生をするためのエディタ拡張
    /// </summary>
    public sealed class AnimationClipPreviewWindow : EditorWindow
    {
        private static readonly Vector2 size = new Vector2(640, 640);

        #region Fields

        private PreviewRenderUtility _previewRenderUtility;

        /// <summary>
        /// 再生用のGraph
        /// </summary>
        private PlayableGraph _graph;

        /// <summary>
        /// 再生先のAnimator
        /// </summary>
        private static Animator _previewAnimator;
        /// <summary>
        /// 再生用GraphのOutput。Animatorと一対
        /// </summary>
        private AnimationPlayableOutput _output;

        /// <summary>
        /// 再生したいAnimationClip
        /// </summary>
        private AnimationClip _previewAnimationClip;
        private AnimationClipPlayable _animationClipPlayable;

        /// <summary>
        /// 確認したいプレハブ
        /// アセットのものだけでなく、シーンのものも対象にする。
        /// 窓を閉じた後も継続したいのでstaticで
        /// </summary>
        private static GameObject _previewObject;
        /// <summary>
        /// 確認用のInstance
        /// </summary>
        private static GameObject _previewObjectInstance;
        /// <summary>
        /// 確認用のInstanceはどのObjectから作られたかのチェック用
        /// </summary>
        private static GameObject _instantiatedOriginalObject;

        #endregion

        /// <summary>
        /// AnimationClipを開いた時のコールバック
        /// </summary>
        [OnOpenAsset(0)]
        public static bool OnOpen(int instanceID, int line)
        {
            //AnimationClip以外は通常
            if (EditorUtility.InstanceIDToObject(instanceID) is not AnimationClip animationClip)
            {
                return false;
            }

            Open(animationClip);
            return true;
        }

        /// <summary>
        /// メニューから開く場合
        /// </summary>
        [MenuItem("Tools/ScreenPocket/AnimationClip Preview")]
        public static void Open()
        {
            Open(null);
        }

        /// <summary>
        /// ダブルクリックから開く場合
        /// </summary>
        /// <param name="clip"></param>
        private static void Open(AnimationClip clip)
        {
            var w = GetWindow<AnimationClipPreviewWindow>("AnimationClip Preview");
            w.minSize = new Vector2(640, 640);
            w.wantsMouseMove = true;
            w._previewAnimationClip = clip;
        }

        private void OnEnable()
        {
            if (_previewRenderUtility == null)
            {
                _previewRenderUtility = new PreviewRenderUtility();

                //ライトの向き設定
                _previewRenderUtility.lights[0].transform.rotation = Quaternion.Euler(45f, 45f, 0f);
            }
        }

        private void OnDisable()
        {
            if (_previewObjectInstance != null)
            {
                DestroyImmediate(_previewObjectInstance);
                _previewObjectInstance = null;
            }

            if (_previewRenderUtility != null)
            {
                _previewRenderUtility.Cleanup();
                _previewRenderUtility = null;
            }

            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
        }

        public void OnGUI()
        {
            _previewAnimationClip = (AnimationClip)EditorGUILayout.ObjectField("AnimationClip", _previewAnimationClip, typeof(AnimationClip), false);
            _previewObject = (GameObject)EditorGUILayout.ObjectField("Prefab", _previewObject, typeof(GameObject), true);
            OnGuiAnimation();

            //プレハブの更新
            UpdatePrefab();

            var rect = new Rect(0, 72, size.x, size.y - 72);

            _previewRenderUtility.BeginPreview(rect, GUIStyle.none);

            //再生用のオブジェクトをprevewRenderUtilityに登録する
            UpdateInstance(_previewRenderUtility);

            //オブジェクトにの状況に応じてPlayableGraphを再構築する
            UpdatePlayableGraph();

            UpdateAnimationClipPlayable();

            UpdateCamera(_previewRenderUtility.camera);

            _previewRenderUtility.Render(true);//←URPならtrueが必要。BIRPならtrueを除去してください
            _previewRenderUtility.EndAndDrawPreview(rect);

            Repaint();
        }

        /// <summary>
        /// Animation操作のGUI
        /// </summary>
        private void OnGuiAnimation()
        {
            if (!_animationClipPlayable.IsValid())
            {
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            var time = EditorGUILayout.Slider("Time", (float)_animationClipPlayable.GetTime(), 0f, (float)_animationClipPlayable.GetDuration());
            if (EditorGUI.EndChangeCheck())
            {
                _animationClipPlayable.SetTime(time);
            }

            if (GUILayout.Button("|<<", GUILayout.Width(28)))
            {
                _animationClipPlayable.SetTime(0);
            }
            if (GUILayout.Button("||", GUILayout.Width(28)))
            {
                _animationClipPlayable.SetSpeed(0);
            }
            if (GUILayout.Button(">", GUILayout.Width(28)))
            {
                _animationClipPlayable.SetSpeed(1);
            }
            if (GUILayout.Button(">>|", GUILayout.Width(28)))
            {
                _animationClipPlayable.SetTime(_animationClipPlayable.GetDuration());
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 確認用のプレハブインスタンスを状況に応じて作り直す
        /// </summary>
        private static void UpdatePrefab()
        {
            //プレハブが切り替わったら
            if (_previewObjectInstance != null && _instantiatedOriginalObject != _previewObject)
            {
                //今表示中のものは消す
                DestroyImmediate(_previewObjectInstance);
                _previewObjectInstance = null;
                //チェック用も除去
                _instantiatedOriginalObject = null;
            }

            if (_previewObjectInstance == null && _previewObject != null)
            {
                //_previewRenderUtility.InstantiatePrefabInScene()はアセット側のプレハブしか対象にしないので、普通のInstantiateで複製
                _previewObjectInstance = Instantiate(_previewObject);

                //無事にInstantiate出来たら、instantiateした元のプレハブを保持しておく
                if (_previewObjectInstance != null)
                {
                    _instantiatedOriginalObject = _previewObject;
                }
            }
        }

        /// <summary>
        /// プレビュー用のInstanceをPreviewRenderUtilityに登録する
        /// </summary>
        private static void UpdateInstance(PreviewRenderUtility utility)
        {
            if (_previewObjectInstance == null)
            {
                return;
            }

            //Preview側のScene登録
            utility.AddSingleGO(_previewObjectInstance);
            //位置はまぁ原点で
            _previewObjectInstance.transform.localPosition = Vector3.zero;
            _previewObjectInstance.transform.localRotation = Quaternion.identity;
            _previewObjectInstance.transform.localScale = Vector3.one;
        }

        /// <summary>
        /// 状況に応じてPlayableGraphを組みなおす
        /// </summary>
        private void UpdatePlayableGraph()
        {
            if (_previewObjectInstance == null)
            {
                return;
            }

            //Animatorを探す。
            //設定されたプレハブのRoot階層に無い可能性があるので子供も含めて探す(プレビュー用だし非アクティブまで探さなくても良い)
            var animator = _previewObjectInstance.GetComponentInChildren<Animator>();

            if (_previewAnimator == animator)
            {
                //変化なしなら抜ける
                return;
            }

            //変化アリなら再生中のGraphは削除
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }

            _previewAnimator = animator;

            //もしAnimatorが無いなら何もしない
            if (_previewAnimator == null)
            {
                return;
            }

            //何か再生したいならGraphを構築
            _graph = PlayableGraph.Create("Animation Preview Window Graph");
            _output = AnimationPlayableOutput.Create(_graph, "Output", _previewAnimator);
            //Outputに未接続だけどとりあえず再生(接続はこの次のUpdateAnimationClipPlayable()で行う)
            _graph.Play();
        }

        /// <summary>
        /// AnimationClipPlayableの更新
        /// </summary>
        private void UpdateAnimationClipPlayable()
        {
            if (!_graph.IsValid())
            {
                return;
            }

            //すでに再生中？
            if (_animationClipPlayable.IsValid())
            {
                //同じなら抜ける
                if (_animationClipPlayable.GetAnimationClip() == _previewAnimationClip)
                {
                    return;
                }

                //違うなら削除
                _animationClipPlayable.Destroy();
            }

            //次再生したいアニメが空っぽなら抜ける
            if (_previewAnimationClip == null)
            {
                return;
            }

            //Playableを作り直し
            _animationClipPlayable = AnimationClipPlayable.Create(_graph, _previewAnimationClip);
            _animationClipPlayable.SetDuration(_previewAnimationClip.length);
            //Outputに接続
            _output.SetSourcePlayable(_animationClipPlayable);
        }

        /// <summary>
        /// カメラの更新
        /// </summary>
        /// <param name="camera"></param>
        private static void UpdateCamera(Camera camera)
        {
            //位置角度などは自由に調整してください。外部からいじれるとなおよい
            camera.farClipPlane = 100;
            camera.transform.position = new Vector3(0, 1f, 5f);
            camera.transform.rotation = Quaternion.Euler(0, 180, 0);
            camera.clearFlags = CameraClearFlags.Skybox;
        }
    }
}