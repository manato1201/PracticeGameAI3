#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using EditorExtension;

[CustomEditor(typeof(MeshRenderer))]
public class MeshRendererEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        ComponentHeaderUtility.DrawHeaderButtons((Component)target);
    }
}
#endif