#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using Unity.Collections;
using UnityEngine.SceneManagement;

using GLTFast;
using GLTFast.Logging;
using GLTFast.Export;
using Newtonsoft.Json.Linq;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

public class ExportJoltGLB : MonoBehaviour
{   

        const string k_GltfExtension = "gltf";
        const string k_GltfBinaryExtension = "glb";

        static string SaveFolderPath
        {
            get
            {
                var saveFolderPath = EditorUserSettings.GetConfigValue("glTF.saveFilePath");
                if (string.IsNullOrEmpty(saveFolderPath))
                {
                    saveFolderPath = Application.streamingAssetsPath;
                }
                return saveFolderPath;
            }
            set => EditorUserSettings.SetConfigValue("glTF.saveFilePath", value);
        }
        static ExportSettings GetDefaultSettings(bool binary)
        {
            var settings = new ExportSettings
            {
                Format = binary ? GltfFormat.Binary : GltfFormat.Json
            };
            return settings;
        }


        static void CreateHeighField(JoltRigidBody.Data body, Terrain terrain, GltfData gltf) {
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
                    uint rgb = (uint) (0xFFFF * normalized);
                    writer.Write((byte) ((rgb >> 8) & 0xFF));
                    writer.Write((byte) ((rgb >> 0) & 0xFF));
                    writer.Write((byte) 0);
                  }
                }
                NativeArray<byte> imageBytes = new NativeArray<byte>(stream.ToArray(), Allocator.Temp);
                byte[] png = ImageConversion.EncodeNativeArrayToPNG(imageBytes, GraphicsFormat.R8G8B8_UNorm, (uint) size, (uint) size).ToArray();
                body.heightfield = new JoltRigidBody.HeightFieldData() {
                  depthBuffer = gltf.AddImage(png, true),
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
                  splatImages.Add(gltf.AddImage(layer, false));
                }
                body.heightfield.splatIndex = splatImages.ToArray();
                List<JoltRigidBody.HeightFieldLayer> layers = new List<JoltRigidBody.HeightFieldLayer>();
                for(var i = 0; i< data.terrainLayers.Length; i++) {
                  var layer = data.terrainLayers[i];
                  layers.Add(new JoltRigidBody.HeightFieldLayer() {
                    name = layer.name,
                    diffuse = gltf.AddImage(layer.diffuseTexture, false),
                    tileOffset = new float[] { layer.tileOffset[0], layer.tileOffset[1] },
                    tileSize = new float[] { layer.tileSize[0], layer.tileSize[1] },
                  });
                }
                body.heightfield.layers = layers.ToArray();
        }

        static void UpdateJoltExtras(JObject node, int nodeIndex, JoltRigidBody.Data body,  List<JObject> constraints) {
          node["extras"] = node["extras"] ?? new JObject();
          var joltExtras = node["extras"]["jolt"] = new JObject();

          joltExtras["id"] = nodeIndex;
          joltExtras["collision"] = JObject.FromObject(body);
          if(constraints.Count > 0) {
            joltExtras["constraints"] = new JArray(constraints.ToArray());
          }
        }

        static void AddJoltData(Dictionary<GameObject, int> nodeMap, GltfData gltf) {
          var allGameObjects = nodeMap.Keys;
          foreach(GameObject obj in allGameObjects) {
            int nodeIndex = nodeMap[obj];
            if (obj.TryGetComponent(out JoltRigidBody rigidBody))
            {
              JObject node = (JObject)gltf.m_Json["nodes"][nodeIndex];
              var jsonConstraints = new List<JObject>();
              JoltRigidBody.Data body = rigidBody.GetData();

              string nodeName = (string) node["name"];
              if(!nodeName.Equals(obj.name)) {
                Debug.LogError("Node Mismatch at index ["+nodeIndex+"] - Expected: ["+obj.name+"] and got ["+nodeName+"]");
              }
              if(rigidBody.m_CollisionShape == JoltCollisionShape.HeightField && obj.TryGetComponent(out Terrain terrain)) {
                CreateHeighField(body, terrain, gltf);
              }
              var constraints = obj.GetComponents<JoltConstraint>();
              if(constraints.Length > 0 && rigidBody != null) 
              {
                for(var i=0;i<constraints.Length; i++) {
                  var constraint = constraints[i];
                  if(constraint.m_Body1 != null) {
                    JoltConstraintData constraintJson = constraint.GetData();
                    constraintJson.type = constraint.m_ConstraintType.ToString();
                    constraintJson.body1 = nodeMap[constraint.m_Body1];
                    jsonConstraints.Add(JObject.FromObject(constraintJson));
                  }
                }
              }
              UpdateJoltExtras(node, nodeIndex, body, jsonConstraints);
              gltf.m_Json["nodes"][nodeIndex] = node;
            }
          }
        } 

        static void AddGameObject(GameObject gameObject, Dictionary<GameObject,int> nodeMap, int lastId, out int nodeId, GameObjectExportSettings gameObjectExportSettings, ExportSettings settings) {
            nodeId = -1;
            if (gameObjectExportSettings.OnlyActiveInHierarchy && !gameObject.activeInHierarchy
                || gameObject.CompareTag("EditorOnly"))
            {
                return;
            }
            var childCount = gameObject.transform.childCount;
            bool hasChildren = false;
            if (childCount > 0)
            {
                for (var i = 0; i < childCount; i++)
                {
                    var child = gameObject.transform.GetChild(i);
                    AddGameObject(child.gameObject, nodeMap, lastId, out var childNodeId, gameObjectExportSettings, settings);
                    if (childNodeId >= 0)
                    {
                      nodeId = lastId = childNodeId;
                      hasChildren = true;
                    }
                }
            }

            var onIncludedLayer = ((1 << gameObject.layer) & gameObjectExportSettings.LayerMask) != 0;

            if (onIncludedLayer || hasChildren)
            {
                nodeMap[gameObject] = lastId;
                nodeId = lastId + 1;

                if (onIncludedLayer)
                {
                    if (gameObject.TryGetComponent(out Camera camera))
                    {
                        if (camera.enabled || gameObjectExportSettings.DisabledComponents)
                        {
                            nodeId = nodeId + 1;
                        }
                    }

                    if (gameObject.TryGetComponent(out Light light))
                    {
                        if (light.enabled || gameObjectExportSettings.DisabledComponents)
                        {
                          if ((settings.ComponentMask & ComponentType.Light) != 0)
                          {
                            if (light.type != LightType.Point)
                            {
                              nodeId = nodeId + 1;
                            }
                          }
                        }
                    }
                }
            }
        }

        static Dictionary<GameObject,int>  MapGameObjects(GameObject[] gameObjects, GameObjectExportSettings gameObjectExportSettings, ExportSettings settings) {
            Dictionary<GameObject,int> nodeMap = new Dictionary<GameObject,int>(); 
            int nodeId = 0;
            for (var index = 0; index < gameObjects.Length; index++)
            {
                var gameObject = gameObjects[index];
                AddGameObject(gameObject, nodeMap, nodeId, out nodeId, gameObjectExportSettings, settings);
            }
            return nodeMap;
        }

        [MenuItem("File/Export Scene/Jolt glTF-Binary (.glb)", false, 174)]
        static void ExportSceneBinaryMenu()
        {
            var scene = SceneManager.GetActiveScene();
            var gameObjects = scene.GetRootGameObjects();
            var extension = k_GltfBinaryExtension;

            var path = EditorUtility.SaveFilePanel(
                "glTF Export Path",
                SaveFolderPath,
                $"{scene.name}.{extension}",
                extension
                );
            if (!string.IsNullOrEmpty(path))
            {
                SaveFolderPath = Directory.GetParent(path)?.FullName;
                var settings = GetDefaultSettings(true);
                var gameObjectExportSettings = new GameObjectExportSettings();
                var export = new GameObjectExport(settings, gameObjectExportSettings, logger: new ConsoleLogger());
                export.AddScene(gameObjects, scene.name);
                var stream = new MemoryStream();
                AsyncHelpers.RunSync(() => export.SaveToStreamAndDispose(stream));
                var gltf = GltfData.FromStream(stream);

                var gameObjectMap = MapGameObjects(gameObjects, gameObjectExportSettings, settings);
                AddJoltData(gameObjectMap, gltf);

                var outStream = new FileStream(path, FileMode.Create);
                gltf.WriteToStream(outStream);
            }
        }
}

#endif
