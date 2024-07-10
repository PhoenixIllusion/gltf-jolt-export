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

public class JoltRigid_extras : IJoltGltfPlugin {
  public bool HasJoltData(GameObject obj) {
    return obj.TryGetComponent(out JoltRigidBody rigidBody) && rigidBody != null;
  }


  void CreateHeighField(JoltRigidBody.Data body, Terrain terrain, JoltGltfUtil gltf) {
    TerrainData data = terrain.terrainData;
    int size = Math.Min(data.heightmapTexture.width, data.heightmapTexture.height);
    var stream = new MemoryStream();
    float[,] heights = data.GetHeights(0,0, size, size);
    var scale = data.heightmapScale;
    float minVal = 1 << 30;
    float maxVal = -1 << 30;
    foreach(float f in heights) {
      if(f > maxVal) maxVal = f;
      if(f < minVal) minVal = f;
    }
    float delta = maxVal - minVal;
    using (BinaryWriter writer = new BinaryWriter(stream))
    {
      foreach(float f in heights) {
        float normalized =  (f - minVal) / maxVal;
        uint rgb = (uint) (0xFFFFFF * normalized);
        writer.Write((byte) ((rgb >> 16) & 0xFF));
        writer.Write((byte) ((rgb >> 8) & 0xFF));
        writer.Write((byte) (rgb >> 0) & 0xFF);
      }
    }

    body.heightfield = new JoltRigidBody.HeightFieldData() {
      depthBuffer = gltf.SetTextureRGB(stream.ToArray(), size, size),
      minHeight = minVal * scale.y,
      maxHeight = maxVal * scale.y,
      colorFilter = new float[] { (float)0xFF00 / (float)0xFFFF, (float)0x00FF/ (float)0xFFFF }
    };
    stream.Close();
    var ext = data.bounds.extents;
    body.extents = new float[]{ ext[0], ext[1], ext[2] };
    var worldMatrix = terrain.transform.localToWorldMatrix;
    var tCenter = worldMatrix.GetPosition();
    var tRotation = worldMatrix.rotation;
    var tScale = worldMatrix.lossyScale;

    body.worldPosition =  new float[]{ tCenter[0], tCenter[1], tCenter[2]};
    body.worldRotation = new float[] { tRotation[0], tRotation[1], tRotation[2], tRotation[3]};
    body.worldScale = new float[]  { tScale[0], tScale[1], tScale[2]};

    List<int> splatImages = new List<int>();
    for(var i = 0; i< data.alphamapTextures.Length; i++) {
      Texture2D layer = data.alphamapTextures[i];
      splatImages.Add(gltf.SetTexture(layer, false));
    }
    body.heightfield.splatIndex = splatImages.ToArray();
    List<JoltRigidBody.HeightFieldLayer> layers = new List<JoltRigidBody.HeightFieldLayer>();
    for(var i = 0; i< data.terrainLayers.Length; i++) {
      var layer = data.terrainLayers[i];
      layers.Add(new JoltRigidBody.HeightFieldLayer() {
        name = layer.name,
        diffuse = gltf.SetTexture(layer.diffuseTexture, false),
        tileOffset = new float[] { layer.tileOffset[0], layer.tileOffset[1] },
        tileSize = new float[] { layer.tileSize[0], layer.tileSize[1] },
      });
    }
    body.heightfield.layers = layers.ToArray();
  }
  public void AddJoltData(JObject joltExtras, GameObject obj, JoltGltfUtil gltf) {
    if (obj.TryGetComponent(out JoltRigidBody rigidBody))
    {
      var jsonConstraints = new List<JObject>();
      JoltRigidBody.Data body = rigidBody.GetData();

      if(rigidBody.m_CollisionShape == JoltCollisionShape.HeightField && obj.TryGetComponent(out Terrain terrain)) {
        CreateHeighField(body, terrain, gltf);
      }

      joltExtras["collision"] = JObject.FromObject(body);
    }
  }
}
#endif