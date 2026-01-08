using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;

namespace ScreenPocket.Editor
{
    /// <summary>
    /// 音声ファイルをダブルクリックしたら別窓で再生をするためのエディタ拡張
    /// </summary>
    public sealed class AudioClipPreviewWindow : EditorWindow
    {
        #region Fields
        /// <summary>
        /// AudioSource保持用オブジェクト
        /// 不可視
        /// </summary>
        private GameObject _previewOwner;
        /// <summary>
        /// 再生用のAudioSource
        /// </summary>
        private AudioSource _previewAudioSource;
        /// <summary>
        /// 再生用のGraph
        /// </summary>
        private PlayableGraph _graph;

        /// <summary>
        /// 再生用GraphのOutput。AudioSourceと一対
        /// </summary>
        private AudioPlayableOutput _output;

        /// <summary>
        /// 再生したいAudioClip
        /// </summary>
        private AudioClip _previewAudioClip;
        private AudioClipPlayable _audioClipPlayable;

        #endregion

        /// <summary>
        /// ダブルクリックのコールバック
        /// </summary>
        [OnOpenAsset(0)]
        public static bool OnOpen(int instanceID, int line)
        {
            //AudioClip以外は通常
            if (EditorUtility.InstanceIDToObject(instanceID) is not AudioClip audioClip)
            {
                return false;
            }

            Open(audioClip);
            return true;
        }

        /// <summary>
        /// メニューから開く場合
        /// この関数は正直不要だけども確認用として残す
        /// </summary>
        [MenuItem("Tools/ScreenPocket/AudioClip Preview")]
        public static void Open()
        {
            Open(null);
        }

        /// <summary>
        /// Dropdown形式で開く
        /// </summary>
        /// <param name="clip"></param>
        private static void Open(AudioClip clip)
        {
            var openPosition = Vector2.zero;
            if (Event.current != null)
            {
                //マウスとまったく同じだとちょっと見づらいので少しだけ右下に位置をずらす
                var positionOffset = new Vector2(64f, 12f);
                openPosition = Event.current.mousePosition + positionOffset;
            }

            var popupPosition = GUIUtility.GUIToScreenPoint(openPosition);
            var window = CreateInstance<AudioClipPreviewWindow>();
            //横幅は、まぁスライダーが隠れてしまわない程度に
            const float windowWidth = 240f;
            //縦幅は適当に(デフォルト行サイズ+α)*３行分くらい
            var windowHeight = (EditorGUIUtility.singleLineHeight + 4) * 3;
            var windowSize = new Vector2(windowWidth, windowHeight);
            //表示
            window.ShowAsDropDown(new Rect(popupPosition, Vector2.zero), windowSize);
            //渡されたクリップを流し込み
            window._previewAudioClip = clip;
            //同じアセットを何度もダブルクリックした時に備えて時間を0に戻しておく
            window.SetTime(0);
        }

        /// <summary>
        /// 時間を指定
        /// </summary>
        /// <param name="time"></param>
        private void SetTime(double time)
        {
            if (!_audioClipPlayable.IsValid())
            {
                return;
            }

            _audioClipPlayable.SetTime(time);
        }

        private void OnEnable()
        {
            _previewOwner = new GameObject("AudioSourceOwnerForPreview")
            {
                hideFlags = HideFlags.HideAndDontSave //非表示化
            };
            _previewAudioSource = _previewOwner.AddComponent<AudioSource>();

            _graph = PlayableGraph.Create("AudioClip Preview Window Graph");
            _output = AudioPlayableOutput.Create(_graph, "Output", _previewAudioSource);
            //Outputに未接続だけどとりあえず再生(接続はこの次の UpdateAudioClipPlayable() で行う)
            _graph.Play();
        }

        /// <summary>
        /// 無効時コールバック
        /// 後片付けを行う
        /// </summary>
        private void OnDisable()
        {
            //片付け
            Cleanup();
        }

        public void OnGUI()
        {
            //ラベル幅を狭く
            EditorGUIUtility.labelWidth = 60;
            _previewAudioClip = (AudioClip)EditorGUILayout.ObjectField("AudioClip", _previewAudioClip, typeof(AudioClip), false);
            //Clipの状況に応じてPlayableを作り直し
            UpdateAudioClipPlayable();
            //操作部
            OnGuiAudioControl();

            //ラベル幅を戻す
            EditorGUIUtility.labelWidth = 0;
            Repaint();
        }

        private void UpdateAudioClipPlayable()
        {
            if (!_graph.IsValid())
            {
                return;
            }

            //すでに再生中？
            if (_audioClipPlayable.IsValid())
            {
                //同じなら抜ける
                if (_audioClipPlayable.GetClip() == _previewAudioClip)
                {
                    return;
                }

                //違うなら削除
                _audioClipPlayable.Destroy();
            }

            //次再生したいアニメが空っぽなら抜ける
            if (_previewAudioClip == null)
            {
                return;
            }

            //Playableを作り直し(確認したいだけなので基本は非ループ)
            _audioClipPlayable = AudioClipPlayable.Create(_graph, _previewAudioClip, looping: false);
            _audioClipPlayable.SetDuration(_previewAudioClip.length);
            //Outputに接続
            _output.SetSourcePlayable(_audioClipPlayable);
        }

        /// <summary>
        /// Audio再生などの操作のGUI
        /// </summary>
        private void OnGuiAudioControl()
        {
            if (!_audioClipPlayable.IsValid())
            {
                return;
            }

            EditorGUI.BeginChangeCheck();
            var time = EditorGUILayout.Slider("Time", (float)_audioClipPlayable.GetTime(), 0f,
                (float)_audioClipPlayable.GetDuration());
            if (EditorGUI.EndChangeCheck())
            {
                SetTime(time);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("|<<", GUILayout.Width(32)))
            {
                SetTime(0);
            }

            if (GUILayout.Button("||", GUILayout.Width(32)))
            {
                _audioClipPlayable.SetSpeed(0);
            }

            if (GUILayout.Button(">", GUILayout.Width(32)))
            {
                _audioClipPlayable.SetSpeed(1);
            }

            if (GUILayout.Button(">>|", GUILayout.Width(32)))
            {
                SetTime(_audioClipPlayable.GetDuration());
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 片付け
        /// </summary>
        private void Cleanup()
        {
            //Playableの削除 GraphのDestroy()でまとめて片付けてくれそうだけども一応
            if (_audioClipPlayable.IsValid())
            {
                _audioClipPlayable.Destroy();
            }

            //Graphの削除
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }

            //再生用オブジェクトの削除
            if (_previewOwner != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_previewOwner);
                }
                else
                {
                    DestroyImmediate(_previewOwner);
                }

                _previewOwner = null;
            }
        }
    }
}