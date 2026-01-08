#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System;

namespace EditorExtension
{
    public static class ComponentHeaderUtility
    {
        private static string copiedJson;
        private static Type copiedType;

        public static void DrawHeaderButtons(Component targetComponent)
        {
            GameObject go = targetComponent.gameObject;
            Type type = targetComponent.GetType();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("C", GUILayout.Width(25)))
            {
                copiedType = type;
                copiedJson = EditorJsonUtility.ToJson(targetComponent);
            }

            GUI.enabled = copiedType != null && copiedType == type;
            if (GUILayout.Button("P", GUILayout.Width(25)))
            {
                Undo.RegisterCompleteObjectUndo(go, "Paste Component");
                var pasted = go.AddComponent(copiedType);
                EditorJsonUtility.FromJsonOverwrite(copiedJson, pasted);
            }
            GUI.enabled = true;

            if (GUILayout.Button("✕", GUILayout.Width(25)))
            {
                copiedType = null;
                copiedJson = null;
            }

            if (GUILayout.Button("R", GUILayout.Width(25)))
            {
                Undo.DestroyObjectImmediate(targetComponent);
                return;
            }

            if (GUILayout.Button("↑", GUILayout.Width(25)))
            {
                ComponentUtility.MoveComponentUp(targetComponent);
            }

            if (GUILayout.Button("↓", GUILayout.Width(25)))
            {
                ComponentUtility.MoveComponentDown(targetComponent);
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif