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

public class Collider_extras : IJoltGltfPlugin {
  public bool HasJoltData(GameObject obj) {
    return obj.TryGetComponent(out Collider collider) && collider != null;
  }

  float[] ToArray(Vector3 v3) {
      return new float[] { v3[0], v3[1], v3[2] };
  }

  float[] ToArray(Quaternion v4) {
      return new float[] { v4[0], v4[1], v4[2], v4[3] };
  }

  public void AddJoltData(JObject joltExtras, GameObject obj, JoltGltfUtil gltf) {
    obj.TryGetComponent(out JoltRigidBody jRigid);
    if(jRigid != null) {
      return;
    }
    if(obj.TryGetComponent(out Collider collider)) 
    {
      var scale =  new float[] { 1, 1, 1 };
      var rotation =  new float[] { 0, 0, 0, 1 };

      var worldMatrix = collider.transform.localToWorldMatrix;
      var worldPosition = worldMatrix.GetPosition();
      var worldRotation = worldMatrix.rotation;
      var worldScale = worldMatrix.lossyScale;
      var localBounds = collider.bounds; 

      var collisionShape = "Mesh";
      if(collider is BoxCollider boxCollider) {
        collisionShape = "Box";
      }
      if(collider is SphereCollider sphereCollider) {
        collisionShape = "Sphere";
      }
      if(collider is CapsuleCollider capsuleCollider) {
        collisionShape = "Capsule";
      }

      Nullable<int> mesh = null;
      if(collider is MeshCollider meshCollider) {
        if(meshCollider.convex) {
          collisionShape = "ConvexHull";
        }
        mesh = gltf.SetMesh(meshCollider.sharedMesh);
      }
      var body = new JoltRigidBody.Data {
            isSensor = collider.isTrigger ? true : null,
            collisionShape = collisionShape,
            worldPosition = ToArray(worldPosition),
            worldScale = (worldScale == Vector3.one) ? null : ToArray(worldScale),
            worldRotation = (worldRotation == Quaternion.identity) ? null : ToArray(worldRotation),
            extents = ToArray(localBounds.extents),
            mesh = mesh
        };
      
      if(obj.TryGetComponent(out Rigidbody rigidBody)) {
        body.mass = rigidBody.mass;
        if(rigidBody.isKinematic) {
          body.motionType = "Kinematic";
        }
        body.centerOfMass = (rigidBody.centerOfMass == Vector3.zero) ? null : ToArray(rigidBody.centerOfMass);
      }
      joltExtras["collision"] = JObject.FromObject(body, new JsonSerializer { NullValueHandling = NullValueHandling.Ignore });
    }
  }
}
#endif
