#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using Unity.Collections;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using GLTF.Schema;
using UnityGLTF;
using UnityGLTF.Plugins;

public class JoltConstraint_extras : IJoltGltfPlugin {
  public bool HasJoltData(GameObject obj) {
    var constraints = obj.GetComponents<JoltConstraint>();
    return constraints.Length > 0;
  }

  public void AddJoltData(JObject joltExtras, GameObject obj, JoltGltfUtil gltf) {
    var constraints = obj.GetComponents<JoltConstraint>();
    if(constraints.Length > 0) 
    {
      List<JObject> jsonConstraints = new();
      for(var i=0;i<constraints.Length; i++) {
        var constraint = constraints[i];
        if(constraint.m_Body1 != null) {
          JoltConstraintData constraintJson = constraint.GetData();
          constraintJson.type = constraint.m_ConstraintType.ToString();
          constraintJson.body1 = gltf.NodeIdFromGameObject(constraint.m_Body1);
          jsonConstraints.Add(JObject.FromObject(constraintJson, new JsonSerializer { NullValueHandling = NullValueHandling.Ignore }));
        }
      }
      joltExtras["constraints"] = new JArray(jsonConstraints.ToArray());
    }
  }
}
#endif
