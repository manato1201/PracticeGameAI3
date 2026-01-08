
#if false
//UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using EditorExtension;

[CustomEditor(typeof(SpriteRenderer))]
public class SpriteRendererEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        ComponentHeaderUtility.DrawHeaderButtons((Component)target);
    }
}
#endif