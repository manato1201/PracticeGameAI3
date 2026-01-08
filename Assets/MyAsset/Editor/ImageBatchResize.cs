// 画像一括リサイズ（実ファイル書き換え/生成）＋ Import設定統一
//
// 対象: Project内のTexture2Dアセット（png/jpg/tga等）
// 出力: PNGで保存（Overwrite or Suffix）
// リサイズ: Stretch / Fit(Pad) / Fill(Crop)
//
// 注意:
// - 実ファイルを変更するので、git管理なら差分が大量に出る。Suffix運用推奨。
// - 既にSpriteAtlas等に入ってる場合、作り直しで参照崩れが起き得る。Overwriteするなら覚悟してやれ。

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Linq;

public class ImageBatchResize: EditorWindow
{
    private enum ResizeMode
    {
        Stretch,         // そのまま指定サイズへ伸縮
        FitWithPadding,  // アスペクト維持して収め、余白を透明で埋める
        FillWithCrop     // アスペクト維持して埋め、はみ出しを中央トリミング
    }

    private enum OutputMode
    {
        Overwrite,      // 元ファイルを上書き（危険）
        CreateWithSuffix // suffix付きで新規生成（安全）
    }

    // Resize
    private int _targetWidth = 512;
    private int _targetHeight = 512;
    private ResizeMode _resizeMode = ResizeMode.FitWithPadding;
    private OutputMode _outputMode = OutputMode.CreateWithSuffix;
    private string _suffix = "_resized";
    private Color _paddingColor = new Color(0, 0, 0, 0); // 透明

    // Import settings
    private bool _applyImportSettings = true;

    private TextureImporterType _importerType = TextureImporterType.Sprite; // Sprite or Default
    private SpriteImportMode _spriteMode = SpriteImportMode.Single;

    private bool _sRGB = true;
    private bool _alphaIsTransparency = true;

    private bool _mipMapEnabled = false;
    private FilterMode _filterMode = FilterMode.Bilinear;
    private TextureWrapMode _wrapMode = TextureWrapMode.Clamp;

    private int _maxTextureSize = 2048;
    private TextureImporterCompression _compression = TextureImporterCompression.Uncompressed;
    private bool _crunchedCompression = false;
    private int _compressionQuality = 50; // 0-100



    private Vector2 _scroll;

    [MenuItem("Tools/Art/Batch Resize + Import Settings")]
    public static void Open()
    {
        var w = GetWindow<ImageBatchResize>("Batch Resize + Import");
        w.minSize = new Vector2(520, 560);
        w.Show();
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.LabelField("Batch Resize + Import Settings", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        DrawSelectionInfo();

        EditorGUILayout.Space(8);
        DrawResizeSection();

        EditorGUILayout.Space(10);
        DrawImportSection();

        EditorGUILayout.Space(12);
        DrawRunSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawSelectionInfo()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Input", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Projectでフォルダ or 画像(Texture2D)を選択してから実行。", EditorStyles.wordWrappedMiniLabel);
        }
    }

    private void DrawResizeSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Resize (Write PNG)", EditorStyles.boldLabel);

            _targetWidth = EditorGUILayout.IntField("Target Width", _targetWidth);
            _targetHeight = EditorGUILayout.IntField("Target Height", _targetHeight);

            _targetWidth = Mathf.Max(1, _targetWidth);
            _targetHeight = Mathf.Max(1, _targetHeight);

            _resizeMode = (ResizeMode)EditorGUILayout.EnumPopup("Resize Mode", _resizeMode);
            if (_resizeMode == ResizeMode.FitWithPadding)
            {
                _paddingColor = EditorGUILayout.ColorField("Padding Color", _paddingColor);
            }

            _outputMode = (OutputMode)EditorGUILayout.EnumPopup("Output Mode", _outputMode);
            if (_outputMode == OutputMode.CreateWithSuffix)
            {
                _suffix = EditorGUILayout.TextField("Suffix", _suffix);
                if (string.IsNullOrWhiteSpace(_suffix)) _suffix = "_resized";
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Overwriteは危険。参照・Atlas・差分管理が壊れる可能性がある。理解してるならどうぞ。",
                    MessageType.Warning
                );
            }
        }
    }

    private void DrawImportSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Import Settings (Optional)", EditorStyles.boldLabel);

            _applyImportSettings = EditorGUILayout.ToggleLeft("Apply Import Settings after resize", _applyImportSettings);
            if (!_applyImportSettings) return;

            _importerType = (TextureImporterType)EditorGUILayout.EnumPopup("Texture Type", _importerType);
            if (_importerType == TextureImporterType.Sprite)
            {
                _spriteMode = (SpriteImportMode)EditorGUILayout.EnumPopup("Sprite Mode", _spriteMode);

            }

            _sRGB = EditorGUILayout.Toggle("sRGB (Color Texture)", _sRGB);
            _alphaIsTransparency = EditorGUILayout.Toggle("Alpha Is Transparency", _alphaIsTransparency);

            _mipMapEnabled = EditorGUILayout.Toggle("Mip Maps", _mipMapEnabled);
            _filterMode = (FilterMode)EditorGUILayout.EnumPopup("Filter Mode", _filterMode);
            _wrapMode = (TextureWrapMode)EditorGUILayout.EnumPopup("Wrap Mode", _wrapMode);

            _maxTextureSize = EditorGUILayout.IntPopup("Max Size", _maxTextureSize,
                new[] { "256", "512", "1024", "2048", "4096", "8192" },
                new[] { 256, 512, 1024, 2048, 4096, 8192 });

            _compression = (TextureImporterCompression)EditorGUILayout.EnumPopup("Compression", _compression);
            _crunchedCompression = EditorGUILayout.Toggle("Crunched", _crunchedCompression);
            _compressionQuality = EditorGUILayout.IntSlider("Compression Quality", _compressionQuality, 0, 100);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("※ ここは“統一するための最低限”だけ入れてる。必要なら増やせる。", EditorStyles.miniLabel);
        }
    }

    private void DrawRunSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Run", EditorStyles.boldLabel);

            if (GUILayout.Button("Process Selected", GUILayout.Height(34)))
            {
                ProcessSelected();
            }
        }
    }

    private void ProcessSelected()
    {
        var targets = CollectTargetTextureAssetPaths();
        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("No targets", "選択からTexture2Dが見つからない。フォルダか画像を選べ。", "OK");
            return;
        }

        if (_targetWidth <= 0 || _targetHeight <= 0)
        {
            EditorUtility.DisplayDialog("Invalid size", "Target sizeが不正。", "OK");
            return;
        }

        try
        {
            AssetDatabase.StartAssetEditing();

            int processed = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                var assetPath = targets[i];
                EditorUtility.DisplayProgressBar("Batch Resize", assetPath, (float)i / targets.Count);

                if (!TryLoadTextureReadable(assetPath, out var tex, out var importer))
                    continue;

                var resized = ResizeTexture(tex, _targetWidth, _targetHeight, _resizeMode, _paddingColor);
                if (resized == null)
                    continue;

                var outPath = GetOutputPath(assetPath);
                WritePngAndImport(outPath, resized);

                UnityEngine.Object.DestroyImmediate(resized);

                // Import settings apply
                if (_applyImportSettings)
                {
                    ApplyImporterSettings(outPath);
                }

                processed++;
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Done", $"Processed: {processed}\n(対象: {targets.Count})", "OK");
        }
        catch (Exception e)
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.StopAssetEditing();
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Error", "処理中に例外。Console見ろ。", "OK");
        }
    }

    private List<string> CollectTargetTextureAssetPaths()
    {
        var result = new List<string>(256);

        var selection = Selection.objects;
        if (selection == null || selection.Length == 0) return result;

        foreach (var obj in selection)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;

            if (AssetDatabase.IsValidFolder(path))
            {
                var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { path });
                foreach (var g in guids)
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    if (IsTextureFile(p)) result.Add(p);
                }
            }
            else
            {
                if (IsTextureFile(path))
                {
                    // t:Texture2Dかどうかの厳密判定
                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (t != null) result.Add(path);
                }
            }
        }

        // 重複除去
        var hs = new HashSet<string>(result);
        result = hs.ToList();
        result.Sort(StringComparer.OrdinalIgnoreCase);

        return result;
    }

    private static bool IsTextureFile(string assetPath)
    {
        var ext = Path.GetExtension(assetPath)?.ToLowerInvariant();
        return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga" || ext == ".psd";
    }

    private bool TryLoadTextureReadable(string assetPath, out Texture2D tex, out TextureImporter importer)
    {
        tex = null;
        importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return false;

        // 一時的にReadableにする（既存設定は戻さない：事故が多いので “統一設定で上書き” する方向に寄せる）
        if (!importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        return tex != null;
    }

    private Texture2D ResizeTexture(Texture2D src, int w, int h, ResizeMode mode, Color padColor)
    {
        // RenderTextureでスケールしてReadPixelsで取り出す（高速＆安定）
        // ただし src が圧縮等で読めない場合があるので isReadable を強制してる
        if (src == null) return null;

        // 元のアスペクト考慮
        float srcW = src.width;
        float srcH = src.height;

        // 描画先（w,h）
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prevRT = RenderTexture.active;

        RenderTexture.active = rt;

        // 背景（padding用）
        GL.Clear(true, true, padColor);

        Rect drawRect = new Rect(0, 0, w, h);

        if (mode != ResizeMode.Stretch)
        {
            float srcAspect = srcW / srcH;
            float dstAspect = (float)w / h;

            if (mode == ResizeMode.FitWithPadding)
            {
                // 収める（短辺合わせ）
                if (srcAspect > dstAspect)
                {
                    // 横長 → 幅フィット
                    float nh = w / srcAspect;
                    float y = (h - nh) * 0.5f;
                    drawRect = new Rect(0, y, w, nh);
                }
                else
                {
                    // 縦長 → 高さフィット
                    float nw = h * srcAspect;
                    float x = (w - nw) * 0.5f;
                    drawRect = new Rect(x, 0, nw, h);
                }
            }
            else if (mode == ResizeMode.FillWithCrop)
            {
                // 埋める（長辺合わせ）
                if (srcAspect > dstAspect)
                {
                    // 横長 → 高さフィットして左右切る
                    float nw = h * srcAspect;
                    float x = (w - nw) * 0.5f;
                    drawRect = new Rect(x, 0, nw, h);
                }
                else
                {
                    // 縦長 → 幅フィットして上下切る
                    float nh = w / srcAspect;
                    float y = (h - nh) * 0.5f;
                    drawRect = new Rect(0, y, w, nh);
                }
            }
        }

        // 描画（Bilinear）
        Graphics.Blit(src, rt); // まず全体を作って…
        // Blitだけだとrect制御できないのでDrawTextureで上書きする
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, w, h, 0);
        Graphics.DrawTexture(drawRect, src);
        GL.PopMatrix();

        var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        outTex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        outTex.Apply();

        RenderTexture.active = prevRT;
        RenderTexture.ReleaseTemporary(rt);

        return outTex;
    }

    private string GetOutputPath(string originalAssetPath)
    {
        if (_outputMode == OutputMode.Overwrite)
        {
            // 元拡張子がpng以外でも pngに変えると参照が崩れるので、上書きは「元がpngの時だけ」推奨
            // ここは容赦なくpng固定にする（統一が目的）
            return Path.ChangeExtension(originalAssetPath, ".png").Replace("\\", "/");
        }

        // Suffix運用：同じフォルダに <name><suffix>.png
        string dir = Path.GetDirectoryName(originalAssetPath)?.Replace("\\", "/") ?? "Assets";
        string name = Path.GetFileNameWithoutExtension(originalAssetPath);
        return $"{dir}/{name}{_suffix}.png";
    }

    private void WritePngAndImport(string assetPath, Texture2D tex)
    {
        if (tex == null) return;

        // Assets相対パス -> 絶対パス
        string absPath = Path.GetFullPath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absPath) ?? "");

        byte[] png = tex.EncodeToPNG();
        File.WriteAllBytes(absPath, png);

        // 書き込み後にImport
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }

    private void ApplyImporterSettings(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        importer.textureType = _importerType;

        if (_importerType == TextureImporterType.Sprite)
        {
            importer.spriteImportMode = _spriteMode;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = _alphaIsTransparency;
        }
        else
        {
            importer.alphaIsTransparency = _alphaIsTransparency;
        }

        importer.sRGBTexture = _sRGB;
        importer.mipmapEnabled = _mipMapEnabled;
        importer.filterMode = _filterMode;
        importer.wrapMode = _wrapMode;

        importer.maxTextureSize = _maxTextureSize;
        importer.textureCompression = _compression;
        importer.crunchedCompression = _crunchedCompression;
        importer.compressionQuality = _compressionQuality;

        // リサイズでpng化してるのでReadableは基本不要（重い）
        importer.isReadable = false;

        importer.SaveAndReimport();
    }
}
