using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ParticleSystem))]
public sealed class ParticleSystemInspector : Editor
{
    private static readonly Type BASE_EDITOR_TYPE = typeof(Editor)
        .Assembly
        .GetType("UnityEditor.ParticleSystemInspector");

    public override void OnInspectorGUI()
    {
        var particleSystem = (ParticleSystem)target;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Play"))
            {
                particleSystem.Play();
            }

            if (GUILayout.Button("Stop"))
            {
                particleSystem.Stop();
            }

            if (GUILayout.Button("Pause"))
            {
                particleSystem.Pause();
            }

            if (GUILayout.Button("Clear"))
            {
                particleSystem.Clear();
            }
        }

        var editor = CreateEditor(particleSystem, BASE_EDITOR_TYPE);

        editor.OnInspectorGUI();
    }
}