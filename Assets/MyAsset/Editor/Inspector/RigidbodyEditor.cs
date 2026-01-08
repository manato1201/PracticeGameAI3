#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using EditorExtension;

[CustomEditor(typeof(Rigidbody))]
public class RigidbodyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        ComponentHeaderUtility.DrawHeaderButtons((Component)target);
    }
}
#endif