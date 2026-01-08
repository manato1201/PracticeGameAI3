// Assets/Editor/UIRaycastDebuggerWindow.cs
// Play Mode中に、GameViewのマウス位置(Input.mousePosition)でUIレイキャストし、ヒット順に一覧表示。
// 追加機能:
// - 特定Canvasフィルタ
// - 原因推定ランキング（スコアリング + 根拠表示）
// - ワンクリック修正（Undo対応）
//
// 依存: UnityEngine.UI, UnityEngine.EventSystems
// 注意: GameView座標取得の都合でPlay Mode専用（まず実用性優先）

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIRaycastDebuggerWindow : EditorWindow
{
    // --------- Settings ----------
    private bool _autoRefresh = true;
    private bool _uiOnly = true;
    private bool _warnCommonBlockers = true;
    private int _maxResults = 30;

    // Canvas filter
    private Canvas _targetCanvas;
    private bool _includeChildCanvases = true;
    private bool _autoPickCanvasFromTopHit = false;

    // Ranking / Fix
    private bool _showRanking = true;
    private int _rankingTopN = 5;
    private bool _showFixes = true;
    private int _fixTransparentTopN = 5;

    // Internal
    private readonly List<RaycastResult> _results = new List<RaycastResult>(64);
    private Vector2 _scroll;
    private double _lastUpdateTime;
    private const double UpdateInterval = 0.10;

    // Ranking cache (recomputed every GUI draw)
    private readonly List<Suspect> _suspects = new List<Suspect>(32);

    [MenuItem("Tools/Debug/UI Raycast Debugger")]
    public static void Open()
    {
        var w = GetWindow<UIRaycastDebuggerWindow>("UI Raycast Debugger");
        w.minSize = new Vector2(640, 420);
        w.Show();
    }

    private void OnEnable() => EditorApplication.update += OnEditorUpdate;
    private void OnDisable() => EditorApplication.update -= OnEditorUpdate;

    private void OnEditorUpdate()
    {
        if (!_autoRefresh) return;
        var t = EditorApplication.timeSinceStartup;
        if (t - _lastUpdateTime < UpdateInterval) return;
        _lastUpdateTime = t;
        Repaint();
    }

    private void OnGUI()
    {
        DrawHeader();
        DrawTogglesRow();
        DrawCanvasFilter();
        DrawActionsRow();

        EditorGUILayout.Space(6);

        DoRaycast();
        DrawStatus();

        EditorGUILayout.Space(6);

        if (_showFixes)
            DrawFixPanel();

        if (_showRanking)
            DrawRankingPanel();

        EditorGUILayout.Space(6);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        DrawResults();
        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("UI Raycast Debugger", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Mouse: GameView (Input.mousePosition) / Raycast: EventSystem.RaycastAll", EditorStyles.miniLabel);
        EditorGUILayout.Space(6);

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Play Mode中に動作します。\nGameView上のマウス位置(Input.mousePosition)を使ってUIレイキャストします。",
                MessageType.Info
            );
        }
    }

    private void DrawTogglesRow()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            _autoRefresh = EditorGUILayout.ToggleLeft("Auto Refresh", _autoRefresh, GUILayout.Width(120));
            _uiOnly = EditorGUILayout.ToggleLeft("UI Only", _uiOnly, GUILayout.Width(80));
            _warnCommonBlockers = EditorGUILayout.ToggleLeft("Warn", _warnCommonBlockers, GUILayout.Width(70));

            _showFixes = EditorGUILayout.ToggleLeft("Fix Panel", _showFixes, GUILayout.Width(90));
            _showRanking = EditorGUILayout.ToggleLeft("Ranking", _showRanking, GUILayout.Width(80));

            GUILayout.FlexibleSpace();
            _maxResults = EditorGUILayout.IntSlider("Max Hits", _maxResults, 5, 100);
        }
    }

    private void DrawCanvasFilter()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Target Canvas Filter", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _targetCanvas = (Canvas)EditorGUILayout.ObjectField("Target Canvas", _targetCanvas, typeof(Canvas), true);
                _includeChildCanvases = EditorGUILayout.ToggleLeft("Include Child Canvases", _includeChildCanvases, GUILayout.Width(170));
                _autoPickCanvasFromTopHit = EditorGUILayout.ToggleLeft("Auto Pick from Top Hit", _autoPickCanvasFromTopHit, GUILayout.Width(170));
            }

            EditorGUILayout.LabelField(
                _targetCanvas == null
                    ? "Filter: OFF (All Canvases)"
                    : $"Filter: ON ({_targetCanvas.name})",
                EditorStyles.miniLabel
            );
        }
    }

    private void DrawActionsRow()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh Now", GUILayout.Height(24), GUILayout.Width(120)))
                Repaint();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Select EventSystem", GUILayout.Height(24), GUILayout.Width(160)))
            {
                if (EventSystem.current != null)
                {
                    Selection.activeGameObject = EventSystem.current.gameObject;
                    EditorGUIUtility.PingObject(EventSystem.current.gameObject);
                }
            }
        }
    }

    private void DoRaycast()
    {
        _results.Clear();
        _suspects.Clear();

        if (!EditorApplication.isPlaying) return;

        var es = EventSystem.current;
        if (es == null) return;

        var mp = Input.mousePosition;
        var ped = new PointerEventData(es) { position = new Vector2(mp.x, mp.y) };
        es.RaycastAll(ped, _results);

        if (_uiOnly)
        {
            for (int i = _results.Count - 1; i >= 0; i--)
            {
                var go = _results[i].gameObject;
                if (go == null) { _results.RemoveAt(i); continue; }
                if (go.GetComponent<Graphic>() == null) _results.RemoveAt(i);
            }
        }

        // Auto pick canvas from top hit
        if (_autoPickCanvasFromTopHit && _targetCanvas == null && _results.Count > 0)
        {
            var top = _results[0].gameObject;
            if (top != null)
            {
                var c = top.GetComponentInParent<Canvas>();
                if (c != null) _targetCanvas = c.rootCanvas;
            }
        }

        // Canvas filter
        if (_targetCanvas != null)
        {
            for (int i = _results.Count - 1; i >= 0; i--)
            {
                var go = _results[i].gameObject;
                if (go == null) { _results.RemoveAt(i); continue; }

                var hitCanvas = go.GetComponentInParent<Canvas>();
                if (hitCanvas == null) { _results.RemoveAt(i); continue; }

                var root = hitCanvas.rootCanvas;
                var targetRoot = _targetCanvas.rootCanvas;

                if (_includeChildCanvases)
                {
                    // targetRoot の子孫Canvasも許可する（rootが一致するか、targetRoot配下か）
                    // Canvasの階層上、rootCanvasが一致するなら同系列。
                    // ただし別rootCanvasになっている場合もあり得るので、Transform階層も見る。
                    if (root != targetRoot && !IsChildOf(hitCanvas.transform, targetRoot.transform))
                        _results.RemoveAt(i);
                }
                else
                {
                    // rootCanvas一致のみ許可
                    if (root != targetRoot)
                        _results.RemoveAt(i);
                }
            }
        }

        if (_results.Count > _maxResults)
            _results.RemoveRange(_maxResults, _results.Count - _maxResults);

        // Ranking compute
        ComputeSuspects();
    }

    private void DrawStatus()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.LabelField("Status: Not Playing");
                return;
            }

            var es = EventSystem.current;
            if (es == null)
            {
                EditorGUILayout.LabelField("Status: EventSystem NOT FOUND");
                EditorGUILayout.HelpBox("Sceneに EventSystem がありません。UIレイキャストできません。", MessageType.Warning);
                return;
            }

            var mp = Input.mousePosition;
            EditorGUILayout.LabelField($"Status: OK  |  EventSystem: {es.name}");
            EditorGUILayout.LabelField($"Mouse (screen px): x={mp.x:0}, y={mp.y:0}");
            EditorGUILayout.LabelField($"Hits: {_results.Count}  |  TargetCanvas: {(_targetCanvas ? _targetCanvas.name : "(All)")}");

            // InputModule表示（原因切り分けに使える）
            var module = es.currentInputModule;
            EditorGUILayout.LabelField($"InputModule: {(module ? module.GetType().Name : "(null)")}");

            // クリックが通らないのにヒットゼロなら入力座標/Canvas/Graphic設定を疑う
            if (_results.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "ヒットなし：UIが無い / RaycastTargetがOFF / Canvasが別 / EventSystem不整合などを疑ってください。",
                    MessageType.Info
                );
            }
        }
    }

    // ------------------ Fix Panel ------------------
    private void DrawFixPanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Quick Fix", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Fix: Disable Raycast on TOP Hit", GUILayout.Height(24)))
                {
                    if (_results.Count > 0)
                    {
                        var go = _results[0].gameObject;
                        if (go != null)
                        {
                            var g = go.GetComponent<Graphic>();
                            if (g != null) ToggleGraphicRaycastTarget(g, false);
                        }
                    }
                }

                if (GUILayout.Button("Fix: Disable Transparent Blockers (Top N)", GUILayout.Height(24)))
                {
                    DisableTransparentBlockersTopN(_fixTransparentTopN);
                }

                if (GUILayout.Button("Fix: Select Blocking CanvasGroup (Top Hit)", GUILayout.Height(24)))
                {
                    SelectBlockingCanvasGroupFromTopHit();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _fixTransparentTopN = EditorGUILayout.IntSlider("Top N", _fixTransparentTopN, 1, 20);
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.LabelField(
                "※ FixはUndo可能。副作用があり得るので、適用後に動作確認してください。",
                EditorStyles.miniLabel
            );
        }
    }

    private void DisableTransparentBlockersTopN(int topN)
    {
        int changed = 0;
        for (int i = 0; i < Mathf.Min(topN, _results.Count); i++)
        {
            var go = _results[i].gameObject;
            if (go == null) continue;

            var g = go.GetComponent<Graphic>();
            if (g == null) continue;

            var c = g.color;
            bool nearlyTransparent = c.a <= 0.01f;

            // 「透明だけどレイキャストON」はかなりの確率で犯人
            if (nearlyTransparent && g.raycastTarget)
            {
                ToggleGraphicRaycastTarget(g, false);
                changed++;
            }
        }

        if (changed == 0)
        {
            ShowNotification(new GUIContent("Transparent blockers not found in Top N."));
        }
        else
        {
            ShowNotification(new GUIContent($"Disabled RaycastTarget on {changed} object(s)."));
        }
    }

    private void SelectBlockingCanvasGroupFromTopHit()
    {
        if (_results.Count == 0) return;
        var go = _results[0].gameObject;
        if (go == null) return;

        // 親を含めてCanvasGroupを辿り、「ブロック要因になりそう」なものを探す
        var groups = go.GetComponentsInParent<CanvasGroup>(true);
        if (groups == null || groups.Length == 0)
        {
            ShowNotification(new GUIContent("CanvasGroup not found."));
            return;
        }

        // interactable=false が最優先で怪しい
        var suspect = groups.FirstOrDefault(x => x != null && !x.interactable)
                   ?? groups.FirstOrDefault(x => x != null && x.blocksRaycasts)
                   ?? groups[0];

        if (suspect != null)
        {
            Selection.activeGameObject = suspect.gameObject;
            EditorGUIUtility.PingObject(suspect.gameObject);
            ShowNotification(new GUIContent($"Selected CanvasGroup: {suspect.name}"));
        }
    }

    // ------------------ Ranking ------------------
    private void DrawRankingPanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Cause Ranking (Heuristic)", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _rankingTopN = EditorGUILayout.IntSlider("Show Top", _rankingTopN, 1, 15);
                GUILayout.FlexibleSpace();
            }

            if (_suspects.Count == 0)
            {
                EditorGUILayout.LabelField("No suspects (no hits / filtered out).", EditorStyles.miniLabel);
                return;
            }

            int count = Mathf.Min(_rankingTopN, _suspects.Count);
            for (int i = 0; i < count; i++)
            {
                var s = _suspects[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"#{i + 1}  Score: {s.Score}", GUILayout.Width(140));
                        EditorGUILayout.LabelField(GetHierarchyPath(s.GameObject), EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Select", GUILayout.Width(60)))
                        {
                            Selection.activeGameObject = s.GameObject;
                            EditorGUIUtility.PingObject(s.GameObject);
                        }
                        if (GUILayout.Button("Fix Raycast OFF", GUILayout.Width(120)))
                        {
                            if (s.Graphic != null) ToggleGraphicRaycastTarget(s.Graphic, false);
                        }
                    }

                    // 根拠（短く）
                    foreach (var reason in s.Reasons.Take(5))
                        EditorGUILayout.LabelField("• " + reason, EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.LabelField(
                "※ ランキングは“推定”。断定ではない。根拠を見て判断してください。",
                EditorStyles.miniLabel
            );
        }
    }

    private void ComputeSuspects()
    {
        _suspects.Clear();
        if (!EditorApplication.isPlaying) return;
        if (_results.Count == 0) return;

        // クリック対象（本来押したいもの）の概念は分からないので、
        // 「最前面のヒットから順に、ブロッカーっぽさ」を採点する。
        for (int i = 0; i < _results.Count; i++)
        {
            var go = _results[i].gameObject;
            if (go == null) continue;

            var g = go.GetComponent<Graphic>();
            if (_uiOnly && g == null) continue;

            var s = new Suspect(go, g);

            // depthが小さいほど手前でヒットしてることが多い（モジュールによるが）
            // ここでは単純に「上位ヒット」を少し加点
            int frontBonus = Mathf.Clamp(20 - i * 3, 0, 20);
            if (frontBonus > 0)
            {
                s.Add(frontBonus, $"Front hit (rank {i + 1})");
            }

            // CanvasGroup影響（親含む）
            var groups = go.GetComponentsInParent<CanvasGroup>(true);
            foreach (var cg in groups)
            {
                if (cg == null) continue;

                if (!cg.interactable)
                    s.Add(40, $"CanvasGroup '{cg.name}' interactable=false (blocks interaction)");
                if (!cg.blocksRaycasts)
                    s.Add(10, $"CanvasGroup '{cg.name}' blocksRaycasts=false (may prevent UI receiving raycasts)");
                if (cg.ignoreParentGroups)
                    s.Add(5, $"CanvasGroup '{cg.name}' ignoreParentGroups=true (behavior may differ)");
            }

            // Graphicの典型ブロッカー条件
            if (g != null)
            {
                if (g.raycastTarget)
                    s.Add(5, "Graphic.raycastTarget=ON");

                // 透明
                var c = g.color;
                if (c.a <= 0.01f && g.raycastTarget)
                    s.Add(30, "Nearly transparent (alpha≈0) but RaycastTarget=ON");

                // でかい（画面ブロッカー疑い）
                var rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    var size = rt.rect.size;
                    bool huge = size.x >= 1000f && size.y >= 600f;
                    if (huge && g.raycastTarget)
                        s.Add(25, "Huge RectTransform and RaycastTarget=ON (screen blocker suspect)");
                }

                // Selectableじゃないのに上位ヒット → “見た目要素が邪魔”疑い
                var selectable = go.GetComponent<Selectable>();
                if (selectable == null && g.raycastTarget && i <= 2)
                    s.Add(15, "Top hit but not Selectable (likely blocking intended button)");

                // Selectableがあってもinteractable=falseなら原因
                if (selectable != null && !selectable.IsInteractable())
                    s.Add(35, $"Selectable '{selectable.GetType().Name}' is NOT interactable");

                // Mask配下で想定外が起こることがある
                if (go.GetComponentInParent<Mask>(true) != null || go.GetComponentInParent<RectMask2D>(true) != null)
                    s.Add(8, "Under Mask/RectMask2D (clipping may affect interaction)");
            }

            // ある程度スコアがあるものだけ候補にする
            if (s.Score >= 20)
                _suspects.Add(s);
        }

        // スコア降順
        _suspects.Sort((a, b) => b.Score.CompareTo(a.Score));
    }

    // ------------------ Results List ------------------
    private void DrawResults()
    {
        if (_results.Count == 0)
        {
            EditorGUILayout.HelpBox("ヒットなし。上のStatus/Rankingを確認してください。", MessageType.None);
            return;
        }

        for (int i = 0; i < _results.Count; i++)
        {
            var r = _results[i];
            var go = r.gameObject;
            if (go == null) continue;

            var graphic = go.GetComponent<Graphic>();
            var cg = go.GetComponentInParent<CanvasGroup>(true); // 親CanvasGroupの影響も見る
            var rt = go.GetComponent<RectTransform>();
            var canvas = go.GetComponentInParent<Canvas>();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // Title
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(34));
                    EditorGUILayout.LabelField(GetHierarchyPath(go), EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        Selection.activeGameObject = go;
                        EditorGUIUtility.PingObject(go);
                    }
                    if (GUILayout.Button("Ping", GUILayout.Width(50)))
                    {
                        EditorGUIUtility.PingObject(go);
                    }
                }

                // Meta
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Canvas: {(canvas ? canvas.name : "(none)")}", GUILayout.Width(240));
                    EditorGUILayout.LabelField($"Depth: {r.depth}", GUILayout.Width(90));
                    EditorGUILayout.LabelField($"Distance: {r.distance:0.###}", GUILayout.Width(120));
                    GUILayout.FlexibleSpace();
                }

                // Rect
                if (rt != null)
                {
                    var size = rt.rect.size;
                    var pos = rt.anchoredPosition;
                    EditorGUILayout.LabelField($"Rect: size=({size.x:0.##},{size.y:0.##})  anchoredPos=({pos.x:0.##},{pos.y:0.##})", EditorStyles.miniLabel);
                }

                // Controls
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (graphic != null)
                    {
                        EditorGUILayout.LabelField($"Graphic: {graphic.GetType().Name}", GUILayout.Width(180));

                        var ray = graphic.raycastTarget;
                        EditorGUILayout.LabelField($"RaycastTarget: {(ray ? "ON" : "OFF")}", GUILayout.Width(140));

                        var next = GUILayout.Toggle(ray, "RaycastTarget", "Button", GUILayout.Width(120));
                        if (next != ray)
                            ToggleGraphicRaycastTarget(graphic, next);

                        if (GUILayout.Button("Fix OFF", GUILayout.Width(70)))
                            ToggleGraphicRaycastTarget(graphic, false);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Graphic: (none)", GUILayout.Width(180));
                    }

                    GUILayout.FlexibleSpace();

                    if (cg != null)
                    {
                        var br = cg.blocksRaycasts;
                        EditorGUILayout.LabelField($"CanvasGroup.blocksRaycasts: {(br ? "ON" : "OFF")}", GUILayout.Width(210));
                        var nextBr = GUILayout.Toggle(br, "BlocksRaycasts", "Button", GUILayout.Width(120));
                        if (nextBr != br)
                            ToggleCanvasGroupBlocksRaycasts(cg, nextBr);
                    }
                }

                // Warnings
                if (_warnCommonBlockers && graphic != null)
                {
                    var msg = GetBlockerWarning(graphic, cg);
                    if (!string.IsNullOrEmpty(msg))
                        EditorGUILayout.HelpBox(msg, MessageType.Warning);
                }
            }
        }
    }

    // ------------------ Helpers / Undo ------------------
    private static void ToggleGraphicRaycastTarget(Graphic g, bool enabled)
    {
        if (g == null) return;
        Undo.RecordObject(g, "Toggle RaycastTarget");
        g.raycastTarget = enabled;
        EditorUtility.SetDirty(g);
    }

    private static void ToggleCanvasGroupBlocksRaycasts(CanvasGroup cg, bool enabled)
    {
        if (cg == null) return;
        Undo.RecordObject(cg, "Toggle CanvasGroup.blocksRaycasts");
        cg.blocksRaycasts = enabled;
        EditorUtility.SetDirty(cg);
    }

    private static bool IsChildOf(Transform t, Transform parent)
    {
        if (t == null || parent == null) return false;
        var cur = t;
        while (cur != null)
        {
            if (cur == parent) return true;
            cur = cur.parent;
        }
        return false;
    }

    private static string GetHierarchyPath(GameObject go)
    {
        var sb = new StringBuilder(128);
        var t = go.transform;
        sb.Insert(0, t.name);
        while (t.parent != null)
        {
            t = t.parent;
            sb.Insert(0, t.name + "/");
        }
        return sb.ToString();
    }

    private static string GetBlockerWarning(Graphic g, CanvasGroup cg)
    {
        var c = g.color;
        bool nearlyTransparent = c.a <= 0.01f;

        var rt = g.GetComponent<RectTransform>();
        bool huge = false;
        if (rt != null)
        {
            var size = rt.rect.size;
            huge = size.x >= 1000f && size.y >= 600f;
        }

        if (cg != null)
        {
            if (!cg.interactable)
                return "Warning: 親CanvasGroupが interactable=false。クリックが通らない原因になりやすい。";
            if (!cg.blocksRaycasts)
                return "Note: 親CanvasGroupが blocksRaycasts=false。UIが反応しない原因になり得る。";
        }

        if (nearlyTransparent && g.raycastTarget && huge)
            return "Warning: ほぼ透明(α≈0)の大きいGraphicが RaycastTarget=ON。全面ブロッカーの典型。";

        if (g.raycastTarget && huge)
            return "Note: 大きいGraphicが RaycastTarget=ON。背面UIをブロックしてないか確認。";

        return null;
    }

    // ------------------ Suspect struct ------------------
    private class Suspect
    {
        public GameObject GameObject { get; }
        public Graphic Graphic { get; }
        public int Score { get; private set; }
        public List<string> Reasons { get; } = new List<string>(8);

        public Suspect(GameObject go, Graphic g)
        {
            GameObject = go;
            Graphic = g;
        }

        public void Add(int score, string reason)
        {
            Score += score;
            if (!string.IsNullOrEmpty(reason))
                Reasons.Add($"{reason} (+{score})");
        }
    }
}
