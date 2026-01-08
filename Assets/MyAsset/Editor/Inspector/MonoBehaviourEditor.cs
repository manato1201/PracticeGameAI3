#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using EditorExtension;

[CanEditMultipleObjects]
[CustomEditor(typeof(MonoBehaviour), true)]
public class MonoBehaviourEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        ComponentHeaderUtility.DrawHeaderButtons((Component)target);
    }
}
#endif