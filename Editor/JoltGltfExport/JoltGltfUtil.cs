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
using static UnityGLTF.GLTFSceneExporter;

public class JoltGltfUtil {
  private GLTFSceneExporter exporter;
  private GLTFRoot root;
  private Dictionary<GameObject, Node> nodeMap;

  public JoltGltfUtil( GLTFSceneExporter exporter, GLTFRoot root, Dictionary<GameObject, Node> nodeMap) {
    this.exporter = exporter;
    this.root = root;
    this.nodeMap = nodeMap;
  }

  public int SetTextureRGB(byte[] data, int width, int height) {
    Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
    tex.LoadRawTextureData(data);
    tex.Apply();
    return SetTexture(tex, false);
  }

  public int SetTexture(Texture2D texture, bool hasAlpha) {
    TextureExportSettings exportSettings = new TextureExportSettings();
    exportSettings.alphaMode = hasAlpha ? TextureExportSettings.AlphaMode.Always : TextureExportSettings.AlphaMode.Never;
    exportSettings.linear = false;
    TextureId id = exporter.ExportTexture(texture, TextureMapType.sRGB, exportSettings);
    return id.Id;
  }

  public int NodeIdFromGameObject(GameObject obj) {
    Node node = nodeMap[obj];
    return root.Nodes.IndexOf(node);
  }

  public JObject CreateJoltExtras(Node node) {
      if(node.Extras == null) {
        node.Extras = new JObject();
      }
      JObject extras = node.Extras as JObject;
      JObject joltExtras = extras["jolt"] as JObject ?? new JObject();
      extras["jolt"] = joltExtras;

      joltExtras["id"] = root.Nodes.IndexOf(node);
      return joltExtras;
  }

}

#endif
