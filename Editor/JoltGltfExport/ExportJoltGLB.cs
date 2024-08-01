#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Reflection;
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

public class JoltPhysicsPlugin: GLTFExportPlugin
{
    public override string DisplayName => "Jolt_Physics_Extension";
    public override string Description => "Allows exporting GLTF with extra Jolt Physics collision and constraint data in the 'Extras' property of the scene graph.";
    public override GLTFExportPluginContext CreateInstance(ExportContext context)
    {
        return new Jolt_Physics_Extension_context();
    } 
}

public interface IJoltGltfPlugin {
  public bool HasJoltData(GameObject obj);
  public void AddJoltData(JObject joltExtras, GameObject obj, JoltGltfUtil gltf);
}

public class Jolt_Physics_Extension_context : GLTFExportPluginContext
{
  private Dictionary<GameObject, Node> nodeMap = new Dictionary<GameObject, Node>();

  private readonly List<IJoltGltfPlugin> plugins = new();
  public Jolt_Physics_Extension_context() {
    foreach (var plugin in TypeCache.GetTypesDerivedFrom<IJoltGltfPlugin>())
    {
        if (plugin.GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null) == null)
            continue;
        plugins.Add(Activator.CreateInstance(plugin) as IJoltGltfPlugin);
    }
  }

  public override void AfterNodeExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot, Transform transform, Node node) {
    GameObject obj = transform.gameObject;

    foreach(IJoltGltfPlugin plugin in plugins)
    {
      if(plugin.HasJoltData(obj)) {
        nodeMap[obj] = node;
      }
    }
  }

  void AddJoltData(JoltGltfUtil gltf) {
    var allGameObjects = nodeMap.Keys;
    foreach(GameObject obj in allGameObjects) {
      Node node = nodeMap[obj];

      JObject joltExtras = gltf.CreateJoltExtras(node);
      foreach(IJoltGltfPlugin plugin in plugins)
      {
        if(plugin.HasJoltData(obj)) {
          plugin.AddJoltData(joltExtras, obj, gltf);
        }
      }
    }
  } 

  public override void AfterSceneExport(GLTFSceneExporter exporter, GLTFRoot root)
  {
    JoltGltfUtil gltf = new JoltGltfUtil(exporter, root, nodeMap);
    AddJoltData(gltf);
    nodeMap.Clear();
  }
}
#endif
