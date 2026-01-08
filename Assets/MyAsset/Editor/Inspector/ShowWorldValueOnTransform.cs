using UnityEditor;
using UnityEngine;

namespace EditorExtension
{
    [CustomEditor(typeof(Transform))]
    public sealed class ShowWorldValueOnTransform : Editor
    {
        private Transform _target = null;

        private static Vector3? _cachedWorldPosition = null;
        private static Vector3? _cachedWorldEulerAngles = null;
        private static Vector3? _cachedLocalPosition = null;
        private static Vector3? _cachedLocalEulerAngles = null;
        private static Vector3? _cachedLocalScale = null;

        private void OnEnable() => _target = target as Transform;

        public override void OnInspectorGUI()
        {
            EditorGUIUtility.labelWidth = EditorGUIUtility.currentViewWidth * 0.2f;

            EditorGUILayout.LabelField("ワールド座標", EditorStyles.boldLabel);
            DoWorldPosition();
            DoWorldEulerAngles();
            DoWorldScale();
            DoWorldQuaternion();

            EditorGUILayout.Space(25);

            EditorGUILayout.LabelField("ローカル座標", EditorStyles.boldLabel);
            DoLocalPosition();
            DoLocalEulerAngles();
            DoLocalScale();
            DoLocalQuaternion();

            EditorGUILayout.Space(25);

            EditorGUILayout.LabelField("ツール", EditorStyles.boldLabel);
            DoResetAllValue();
            EditorGUILayout.Space(5);
            DoClearAllCache();

            EditorGUIUtility.labelWidth = EditorGUIUtility.currentViewWidth;
        }

        private void DoWorldPosition()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("C", GUILayout.Width(20)))
            {
                _cachedWorldPosition = _target.position;
                _cachedWorldEulerAngles = null;
                _cachedLocalPosition = null;
                _cachedLocalEulerAngles = null;
                _cachedLocalScale = null;
            }
            GUI.enabled = _cachedWorldPosition != null;
            if (GUILayout.Button("P", GUILayout.Width(20)))
            {
                Undo.RecordObject(_target, "Change World Position");
                _target.position = _cachedWorldPosition ?? default;
            }
            if (GUI.enabled is false) GUI.enabled = true;
            Vector3 newWorldPosition = EditorGUILayout.Vector3Field("Position", _target.position);
            if (newWorldPosition != _target.position)
            {
                Undo.RecordObject(_target, "Change World Position");
                _target.position = newWorldPosition;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DoWorldEulerAngles()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("C", GUILayout.Width(20)))
            {
                _cachedWorldEulerAngles = _target.eulerAngles;
                _cachedWorldPosition = null;
                _cachedLocalPosition = null;
                _cachedLocalEulerAngles = null;
                _cachedLocalScale = null;
            }
            GUI.enabled = _cachedWorldEulerAngles != null;
            if (GUILayout.Button("P", GUILayout.Width(20)))
            {
                Undo.RecordObject(_target, "Change World EulerAngles");
                _target.eulerAngles = _cachedWorldEulerAngles ?? default;
            }
            if (GUI.enabled is false) GUI.enabled = true;
            Vector3 newWorldEulerAngles = EditorGUILayout.Vector3Field("EulerAngles", _target.eulerAngles);
            if (newWorldEulerAngles != _target.eulerAngles)
            {
                Undo.RecordObject(_target, "Change World EulerAngles");
                _target.eulerAngles = newWorldEulerAngles;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DoWorldScale()
        {
            _ = EditorGUILayout.Vector3Field("Scale", _target.lossyScale);
        }

        private void DoWorldQuaternion()
        {
            _ = EditorGUILayout.Vector4Field("Quaternion", _target.rotation.ToVector4());
        }

        private void DoLocalPosition()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("C", GUILayout.Width(20)))
            {
                _cachedLocalPosition = _target.localPosition;
                _cachedWorldPosition = null;
                _cachedWorldEulerAngles = null;
                _cachedLocalEulerAngles = null;
                _cachedLocalScale = null;
            }
            GUI.enabled = _cachedLocalPosition != null;
            if (GUILayout.Button("P", GUILayout.Width(20)))
            {
                Undo.RecordObject(_target, "Change Local Position");
                _target.localPosition = _cachedLocalPosition ?? default;
            }
            if (GUI.enabled is false) GUI.enabled = true;
            Vector3 newLocalPosition = EditorGUILayout.Vector3Field("Position", _target.localPosition);
            if (newLocalPosition != _target.localPosition)
            {
                Undo.RecordObject(_target, "Change Local Position");
                _target.localPosition = newLocalPosition;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DoLocalEulerAngles()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("C", GUILayout.Width(20)))
            {
                _cachedLocalEulerAngles = _target.localEulerAngles;
                _cachedWorldPosition = null;
                _cachedWorldEulerAngles = null;
                _cachedLocalPosition = null;
                _cachedLocalScale = null;
            }
            GUI.enabled = _cachedLocalEulerAngles != null;
            if (GUILayout.Button("P", GUILayout.Width(20)))
            {
                Undo.RecordObject(_target, "Change Local EulerAngles");
                _target.localEulerAngles = _cachedLocalEulerAngles ?? default;
            }
            if (GUI.enabled is false) GUI.enabled = true;
            Vector3 newLocalEulerAngles = EditorGUILayout.Vector3Field("EulerAngles", _target.localEulerAngles);
            if (newLocalEulerAngles != _target.localEulerAngles)
            {
                Undo.RecordObject(_target, "Change Local EulerAngles");
                _target.localEulerAngles = newLocalEulerAngles;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DoLocalScale()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("C", GUILayout.Width(20)))
            {
                _cachedLocalScale = _target.localScale;
                _cachedWorldPosition = null;
                _cachedWorldEulerAngles = null;
                _cachedLocalPosition = null;
                _cachedLocalEulerAngles = null;
            }
            GUI.enabled = _cachedLocalScale != null;
            if (GUILayout.Button("P", GUILayout.Width(20)))
            {
                Undo.RecordObject(_target, "Change Local Scale");
                _target.localScale = _cachedLocalScale ?? default;
            }
            if (GUI.enabled is false) GUI.enabled = true;
            Vector3 newLocalScale = EditorGUILayout.Vector3Field("Scale", _target.localScale);
            if (newLocalScale != _target.localScale)
            {
                Undo.RecordObject(_target, "Change Local Scale");
                _target.localScale = newLocalScale;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DoLocalQuaternion()
        {
            _ = EditorGUILayout.Vector4Field("Quaternion", _target.localRotation.ToVector4());
        }

        private void DoResetAllValue()
        {
            if (GUILayout.Button("デフォルトの値にリセット"))
            {
                Undo.RecordObject(_target, "Reset All Values to Default");
                _target.localPosition = Vector3.zero;
                _target.localEulerAngles = Vector3.zero;
                _target.localScale = Vector3.one;
            }
        }

        private void DoClearAllCache()
        {
            if (GUILayout.Button("コピーした値をクリア"))
            {
                _cachedWorldPosition = null;
                _cachedWorldEulerAngles = null;
                _cachedLocalPosition = null;
                _cachedLocalEulerAngles = null;
                _cachedLocalScale = null;
            }
        }
    }

    public static class ShowWorldValueOnTransformEx
    {
        public static Quaternion ToQuaternion(this Vector4 v) => new(v.x, v.y, v.z, v.w);
        public static Vector4 ToVector4(this Quaternion q) => new(q.x, q.y, q.z, q.w);
    }
}
