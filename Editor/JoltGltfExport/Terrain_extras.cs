#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
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

class TerrainEntry {
  public float height { get; set;}
  public bool hole { get; set; }
}

public class Terrain_extras : IJoltGltfPlugin {
  public bool HasJoltData(GameObject obj) {
    obj.TryGetComponent(out JoltRigidBody rigidBody);
    obj.TryGetComponent(out Terrain terrain);
    return rigidBody == null && terrain != null;
  }

  public static void addHoleData(JoltRigidBody.Data body, TerrainData data, JoltGltfUtil gltf) {
    bool hasHoles = false;
    int size = Math.Min(data.heightmapTexture.width, data.heightmapTexture.height);
    bool[,] holes = data.GetHoles(0, 0, size-1, size-1);
    foreach(bool b in holes) {
      if(b) {
        hasHoles = true;
      }
    }
    if(hasHoles) {
      body.heightfield.holes = gltf.SetTexture(data.holesTexture, true);
    }
  }

  public static JoltRigidBody.TerrainDetail getDetailLayer(DetailPrototype prototype, int densityMap, JoltGltfUtil gltf) {
    var result = new JoltRigidBody.TerrainDetail() {
      densityMap = densityMap
    };
    if(prototype.usePrototypeMesh) {
      var filter = prototype.prototype.GetComponent<MeshFilter>();
      if (filter)
      {
        result.mesh = gltf.SetMesh(filter.sharedMesh);
      }
    } else {
       result.billboard = new JoltRigidBody.TerrainBillboard() {
        texture = gltf.SetTexture(prototype.prototypeTexture, true),
        min = new float[] { prototype.minHeight, prototype.maxHeight },
        max = new float[] { prototype.minWidth, prototype.maxWidth }
      };
    }
    return result;
  }


  public static void addDetailData(JoltRigidBody.Data body, TerrainData data, JoltGltfUtil gltf) {
    int width = data.detailWidth;
    int height = data.detailHeight;
    int[] supportedDetailLayers = data.GetSupportedLayers(0, 0, width, height);
    if(supportedDetailLayers.Length > 0) {
      List<JoltRigidBody.TerrainDetail> layers = new ();
      int index = 0;
      foreach(int layer in supportedDetailLayers) {
        var map = data.GetDetailLayer(0, 0, width, height, layer);
        var stream = new MemoryStream();
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
          for(int y = 0; y < height; y++)
          for(int x = 0 ;x < width; x++) {
            int density = map[y, x];
            writer.Write((byte) (density & 0xFF));
          }
        }
        NativeArray<byte> imageBytes = new NativeArray<byte>(stream.ToArray(), Allocator.Temp);
        byte[] png = ImageConversion.EncodeNativeArrayToPNG(imageBytes, GraphicsFormat.R8_UNorm, (uint) width, (uint) height).ToArray();
        stream.Close();
        var densityMap = gltf.ExportRawImage("detail-layer-"+(index++), png, "image/png");
        var prototype = data.detailPrototypes[layer];

        layers.Add(getDetailLayer(prototype, densityMap, gltf));
      }
    }
  }

  public static void addSplatData(JoltRigidBody.Data body, TerrainData data, JoltGltfUtil gltf) {
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

  public static void CreateHeighField(JoltRigidBody.Data body, Terrain terrain, JoltGltfUtil gltf) {
    TerrainData data = terrain.terrainData;
    int size = Math.Min(data.heightmapTexture.width, data.heightmapTexture.height);
    float[,] heights = data.GetHeights(0, 0, size, size);
    var scale = data.heightmapScale;
    float minVal = 1 << 30;
    float maxVal = -1 << 30;
    foreach(float f in heights) {
      if(f > maxVal) maxVal = f;
      if(f < minVal) minVal = f;
    }
    float delta = maxVal - minVal;
    var stream = new MemoryStream();
    using (BinaryWriter writer = new BinaryWriter(stream))
    {
      for(int y = 0; y < heights.GetLength(0); y++)
      for(int x = 0 ;x < heights.GetLength(1); x++) {
        float height = heights[y, x];
        float normalized =  (height - minVal) / maxVal;
        uint rgb = (uint) (0xFFFF * normalized);
        writer.Write((byte) ((rgb >> 8) & 0xFF));
        writer.Write((byte) ((rgb >> 0) & 0xFF));
        writer.Write((byte) 0);
      }
    }
    NativeArray<byte> imageBytes = new NativeArray<byte>(stream.ToArray(), Allocator.Temp);
    byte[] png = ImageConversion.EncodeNativeArrayToPNG(imageBytes, GraphicsFormat.R8G8B8_UNorm, (uint) size, (uint) size).ToArray();
    stream.Close();

    body.heightfield = new JoltRigidBody.HeightFieldData() {
      depthBuffer = gltf.ExportRawImage("terrain_depth", png, "image/png"),
      minHeight = minVal * scale.y,
      maxHeight = maxVal * scale.y,
      colorFilter = new float[] { (float)0xFF00 / (float)0xFFFF, (float)0x00FF/ (float)0xFFFF }
    };

    addHoleData(body, data, gltf);
    addDetailData(body, data, gltf);
    addSplatData(body, data, gltf);

    var ext = data.bounds.extents;
    body.extents = new float[]{ ext[0], ext[1], ext[2] };
    var worldMatrix = terrain.transform.localToWorldMatrix;
    var tCenter = worldMatrix.GetPosition();
    var tRotation = worldMatrix.rotation;
    var tScale = worldMatrix.lossyScale;

    body.worldPosition =  new float[]{ tCenter[0], tCenter[1], tCenter[2]};
    body.worldRotation = new float[] { tRotation[0], tRotation[1], tRotation[2], tRotation[3]};
    body.worldScale = new float[]  { tScale[0], tScale[1], tScale[2]};

  }


  public void AddJoltData(JObject joltExtras, GameObject obj, JoltGltfUtil gltf) {
    if (obj.TryGetComponent(out Terrain terrain))
    {
      JoltRigidBody.Data body = new JoltRigidBody.Data();

      CreateHeighField(body, terrain, gltf);

      joltExtras["collision"] = JObject.FromObject(body, new JsonSerializer { NullValueHandling = NullValueHandling.Ignore });
    }
  }
}

#endif