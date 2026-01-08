#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using EditorExtension;

[CustomEditor(typeof(BoxCollider2D))]
public class BoxCollider2DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        ComponentHeaderUtility.DrawHeaderButtons((Component)target);
    }
}
#endif