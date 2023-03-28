//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEditor;

//[CustomEditor(typeof(AI))]
//public class EvaluationEditor : Editor
//{
//    public override void OnInspectorGUI()
//    {
//        base.OnInspectorGUI();
//        AI ai = (AI)target;

//        if (GUILayout.Button("Set Values To Inspector"))
//        {
//            ai.version1.SetAllValuesToInspector();
//            ai.version2.SetAllValuesToInspector();
//        }

//        if (GUILayout.Button("Reset Values To Default"))
//        {
//            ai.version1.ResetAllValues();
//            ai.version2.ResetAllValues();
//        }
//    }
//}
