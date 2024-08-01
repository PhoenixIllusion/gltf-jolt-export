#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using Unity.Collections;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using GLTF.Schema;
using UnityGLTF;
using UnityGLTF.Plugins;

public class JoltRigid_extras : IJoltGltfPlugin {
  public bool HasJoltData(GameObject obj) {
    return obj.TryGetComponent(out JoltRigidBody rigidBody) && rigidBody != null;
  }

  public void AddJoltData(JObject joltExtras, GameObject obj, JoltGltfUtil gltf) {
    if (obj.TryGetComponent(out JoltRigidBody rigidBody))
    {
      JoltRigidBody.Data body = rigidBody.GetData();
      if(rigidBody.m_CollisionShape == JoltCollisionShape.ConvexHull || rigidBody.m_CollisionShape == JoltCollisionShape.Mesh) {
        if(rigidBody.m_Mesh != null) {
          body.mesh = gltf.SetMesh(rigidBody.m_Mesh);
        }
      }

      if(rigidBody.m_CollisionShape == JoltCollisionShape.HeightField && obj.TryGetComponent(out Terrain terrain)) {
        Terrain_extras.CreateHeighField(body, terrain, gltf);
      }

      joltExtras["collision"] = JObject.FromObject(body, new JsonSerializer { NullValueHandling = NullValueHandling.Ignore });
    }
  }
}
#endif