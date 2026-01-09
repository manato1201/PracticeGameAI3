#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// あなたが作ったRandomLayoutPlacerの名前空間があるなら合わせて変更してね。
// 例: namespace Group06 { ... } にあるなら using Group06; が必要
// ここではグローバルにある想定で書いてる。

[CustomEditor(typeof(RandomLayoutPlacer))]
public class RandomLayoutPlacerEditor : Editor
{
    private bool includeInactive = true;
    private bool sortByName = true;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 元のInspector
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("SpawnPoints Auto Assign", EditorStyles.boldLabel);

        includeInactive = EditorGUILayout.ToggleLeft("Include Inactive", includeInactive);
        sortByName = EditorGUILayout.ToggleLeft("Sort By Name (otherwise hierarchy order)", sortByName);

        EditorGUILayout.HelpBox(
            "使い方:\n" +
            "1) HierarchyでSpawnPointsの親オブジェクト（256個をまとめた親）を選択\n" +
            "2) 下のボタンを押す\n" +
            "※選択してない場合は、LayoutRandomizer直下の子 'SpawnPoints' を探します",
            MessageType.Info);

        if (GUILayout.Button("Collect SpawnPoints (from Selected Root or Child Named 'SpawnPoints')"))
        {
            var placer = (RandomLayoutPlacer)target;
            Transform root = Selection.activeTransform;

            if (root == null)
            {
                // 選択が無いなら、placer直下の "SpawnPoints" を探す
                var child = placer.transform.Find("SpawnPoints");
                if (child != null) root = child;
            }

            if (root == null)
            {
                Debug.LogError("[RandomLayoutPlacerEditor] 収集元が見つかりません。SpawnPointsの親を選択してから押してください。");
            }
            else
            {
                CollectFromRoot(root);
            }
        }

        if (GUILayout.Button("Clear SpawnPoints"))
        {
            ClearSpawnPoints();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void CollectFromRoot(Transform root)
    {
        var spProp = serializedObject.FindProperty("spawnPoints");
        if (spProp == null)
        {
            Debug.LogError("[RandomLayoutPlacerEditor] 'spawnPoints' フィールドが見つかりません。RandomLayoutPlacerの変数名を確認してください。");
            return;
        }

        // 収集
        var list = new List<Transform>(512);
        var all = root.GetComponentsInChildren<Transform>(includeInactive);

        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            if (t == root) continue;               // 親自身は除外
            if (t == ((RandomLayoutPlacer)target).transform) continue; // placer自身も念のため除外
            list.Add(t);
        }

        if (sortByName)
        {
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        // 反映（Undo対応）
        Undo.RecordObject(target, "Collect SpawnPoints");

        spProp.ClearArray();
        spProp.arraySize = list.Count;

        for (int i = 0; i < list.Count; i++)
        {
            spProp.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);

        Debug.Log($"[RandomLayoutPlacerEditor] Collected SpawnPoints: {list.Count} from root '{root.name}'.");
    }

    private void ClearSpawnPoints()
    {
        var spProp = serializedObject.FindProperty("spawnPoints");
        if (spProp == null) return;

        Undo.RecordObject(target, "Clear SpawnPoints");
        spProp.ClearArray();
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }
}
#endif
