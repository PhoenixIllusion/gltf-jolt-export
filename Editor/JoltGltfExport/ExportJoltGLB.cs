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

        static bool ValidateMagic(Span<byte> span, byte d, byte c, byte b, byte a) {
          if( span[0] == a && span[1] == b && span[2] == c && span[3] == d) {
            return true;
          }
          return false;
        }

        static uint ReadUInt32(Span<byte> span) {
          byte[] bytes = span.ToArray();

          if (!BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
          return BitConverter.ToUInt32(bytes, 0);
        }

        static Span<byte> WriteUInt32(int val) {
          byte[] bytes = BitConverter.GetBytes(val);
          if (!BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
          return new Span<byte>(bytes);
        }

        static byte[] EncodeTexture(RenderTexture texture, bool usePNG)
        {
          RenderTexture.active = texture;
          Texture2D exportTexture = new Texture2D(texture.width, texture.height,
            SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8_UNorm, GraphicsFormatUsage.Sample) ?  GraphicsFormat.R8G8B8_UNorm : GraphicsFormat.R8G8B8A8_UNorm,
            TextureCreationFlags.DontInitializePixels | TextureCreationFlags.DontUploadUponCreate);
          exportTexture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
          exportTexture.Apply();
          var imageData = usePNG ? exportTexture.EncodeToPNG() : exportTexture.EncodeToJPG(75);
          Object.DestroyImmediate(exportTexture);
          RenderTexture.active = null;
          return imageData;
        }

        static byte[] EncodeTexture(Texture2D texture, bool usePNG)
        {
          if(!texture.isReadable) {
                  var destRenderTexture = RenderTexture.GetTemporary(
                    texture.width,
                    texture.height,
                    0,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear,
                    1,
                    RenderTextureMemoryless.Depth
                );
                Graphics.Blit(texture, destRenderTexture);
            var data = EncodeTexture(destRenderTexture, usePNG);
            RenderTexture.ReleaseTemporary(destRenderTexture);
            return data;
          }

          var imageData = usePNG ? texture.EncodeToPNG() : texture.EncodeToJPG(75);
          return imageData;
        }

        static int GetBufferLength(JArray bufferViews) {
          int cur_bin_length = 0;
          int count = bufferViews.Count;
          if(count > 0) {
            var lastEntry = (JObject) bufferViews.Last;
            int byteLength = (int) lastEntry["byteLength"];
            int byteOffset = (int) lastEntry["byteOffset"];
            cur_bin_length = byteLength + byteOffset;
          }
          return cur_bin_length;
        } 

        static int AddBufferView(JArray bufferViews, byte[] binary, List<byte[]> bin_data) {
          int cur_bin_length = GetBufferLength(bufferViews);
          int count = bufferViews.Count;
          bin_data.Add(binary);
          JObject bufferView = new JObject();
          bufferView["buffer"] = 0;
          bufferView["byteOffset"] = cur_bin_length;
          bufferView["byteLength"] = binary.Length;
          bufferViews.Add(bufferView);
          return count;
        }

        static int AddImage(JArray images, bool usePNG, JArray bufferViews, Texture2D texture, List<byte[]> bin_data) {
          return AddImage(images, usePNG, bufferViews, EncodeTexture(texture, usePNG), bin_data);
        }
        static int AddImage(JArray images, bool usePNG, JArray bufferViews, byte[] texture, List<byte[]> bin_data) {
          int count = images.Count;
          int buffer_index = AddBufferView(bufferViews, texture, bin_data);
          JObject image = new JObject();
          image["mimeType"] = usePNG ? "image/png" : "image/jpeg";
          image["bufferView"] = buffer_index;
          images.Add(image);
          return count;
        }

        static void CreateHeighField(JoltRigidBody.Data body, Terrain terrain, List<byte[]> bin_data, JArray bufferViews, JArray images) {
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
                  depthBuffer = AddImage(images, true, bufferViews, png, bin_data),
                  minHeight = minVal * scale.y,
                  maxHeight = maxVal * scale.y,
                  colorFilter = new float[] { (float)0xFF00 / (float)0xFFFF, (float)0x00FF/ (float)0xFFFF }
                };
                var center = data.bounds.center;
                var ext = data.bounds.extents;
                body.extents = new float[]{ ext[0], ext[1], ext[2] };
                body.worldPosition =  new float[]{ center[0],center[1],center[2]};
                body.worldRotation = new float[] { 0, 0, 0, 1 };
                body.worldScale = new float[] { 1, 1, 1};

                List<int> splatImages = new List<int>();
                for(var i = 0; i< data.alphamapTextures.Length; i++) {
                  Texture2D layer = data.alphamapTextures[i];
                  splatImages.Add(AddImage(images, false, bufferViews, layer, bin_data));
                }
                body.heightfield.splatIndex = splatImages.ToArray();
                List<JoltRigidBody.HeightFieldLayer> layers = new List<JoltRigidBody.HeightFieldLayer>();
                for(var i = 0; i< data.terrainLayers.Length; i++) {
                  var layer = data.terrainLayers[i];
                  layers.Add(new JoltRigidBody.HeightFieldLayer() {
                    name = layer.name,
                    diffuse = AddImage(images, false, bufferViews, layer.diffuseTexture, bin_data),
                    tileOffset = new float[] { layer.tileOffset[0], layer.tileOffset[1] },
                    tileSize = new float[] { layer.tileSize[0], layer.tileSize[1] },
                  });
                }
                body.heightfield.layers = layers.ToArray();
                stream.Close();
        }

        static void AddJoltData(Dictionary<GameObject, int> nodeMap, JObject json, List<byte[]> bin_data) {
          var allGameObjects = nodeMap.Keys;
          JArray jImages = (JArray) json["images"] ?? new JArray();
          JArray jBufferViews = (JArray) json["bufferViews"] ?? new JArray();
          foreach(GameObject obj in allGameObjects) {
            int nodeIndex = nodeMap[obj];
            JObject node;
            if (obj.TryGetComponent(out JoltRigidBody rigidBody))
            {
              node = (JObject)json["nodes"][nodeIndex];
              string nodeName = (string) node["name"];
              if(!nodeName.Equals(obj.name)) {
                Debug.LogError("Node Mismatch at index ["+nodeIndex+"] - Expected: ["+obj.name+"] and got ["+nodeName+"]");
              }
              node["extras"] = node["extras"] ?? new JObject();
              node["extras"]["jolt"] = new JObject();
              var joltExtras = node["extras"]["jolt"];
              joltExtras["id"] = nodeIndex;
              JoltRigidBody.Data body = rigidBody.GetData();
              if(rigidBody.m_CollisionShape == JoltCollisionShape.HeightField && obj.TryGetComponent(out Terrain terrain)) {
                CreateHeighField(body, terrain, bin_data, jBufferViews, jImages);
              }
              joltExtras["collision"] = JObject.FromObject(body);
              json["nodes"][nodeIndex] = node;
            }
            var constraints = obj.GetComponents<JoltConstraint>();
            if(constraints.Length > 0 && rigidBody != null) 
            {
              node = (JObject)json["nodes"][nodeIndex];
              node["extras"] = node["extras"] ?? new JObject();
              node["extras"]["jolt"] = node["extras"]["jolt"] ?? new JObject();
              var joltExtras = node["extras"]["jolt"];
              joltExtras["id"] = nodeIndex;
              var jsonConstraints = new List<JObject>();
              for(var i=0;i<constraints.Length; i++) {
                var constraint = constraints[i];
                if(constraint.m_Body1 != null) {
                  var constraintJson = constraint.GetData();
                  constraintJson["type"] = constraint.m_ConstraintType.ToString();
                  constraintJson["body1"] = nodeMap[constraint.m_Body1];
                  jsonConstraints.Add(constraintJson);
                }
              }
              joltExtras["constraints"] = new JArray(jsonConstraints.ToArray());
              json["nodes"][nodeIndex] = node;
            }
          }
          json["bufferViews"] = jBufferViews;
          json["images"] = jImages;
          json["buffers"][0]["byteLength"] = GetBufferLength(jBufferViews);
        } 

        static Span<byte> ModifyJSON(Dictionary<GameObject, int> nodeMap, Span<byte> json_bytes, List<byte[]> bin_data, int cur_bin_length) {
            var utf8 = new UTF8Encoding();
            var json_string = utf8.GetString(json_bytes.ToArray());
            JObject json = JObject.Parse(json_string);
            AddJoltData(nodeMap, json, bin_data);
            json_string = json.ToString(Newtonsoft.Json.Formatting.None);
            var length = json_string.Length;
            var json_padding = "";
            if(length % 4 != 0)
            for(var i=0;i< 4 - (length % 4);i++) {
              json_padding += " ";
            }
            json_string += json_padding;
            length = json_string.Length;

            var bytes = new byte[length];
            utf8.GetBytes(json_string, 0, length, bytes, 0);
            return new Span<byte>(bytes);
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

        static void MapGameObjects(GameObject[] gameObjects, Dictionary<GameObject,int> nodeMap, GameObjectExportSettings gameObjectExportSettings, ExportSettings settings) {
            int nodeId = 0;
            for (var index = 0; index < gameObjects.Length; index++)
            {
                var gameObject = gameObjects[index];
                AddGameObject(gameObject, nodeMap, nodeId, out nodeId, gameObjectExportSettings, settings);
            }
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

                var header_magic = new Span<byte>(new byte[4]);
                var header_version = new Span<byte>(new byte[4]);
                var header_doc_length = new Span<byte>(new byte[4]);

                var json_chunk_length = new Span<byte>(new byte[4]);
                var json_chunk_type = new Span<byte>(new byte[4]);

                var bin_chunk_length = new Span<byte>(new byte[4]);
                var bin_chunk_type = new Span<byte>(new byte[4]);

                stream.Seek(0, SeekOrigin.Begin);

                stream.Read(header_magic);
                stream.Read(header_version);
                stream.Read(header_doc_length);
                if(!ValidateMagic(header_magic, 0x46, 0x54, 0x6C, 0x67)) {
                  Debug.LogError("GLTF Magic Mismatch");
                }
                if(!ValidateMagic(header_version, 0x00, 0x00, 0x00, 0x02)) {
                  Debug.LogError("GLTF Version Not 2");
                }

                stream.Read(json_chunk_length);
                stream.Read(json_chunk_type);
                if(!ValidateMagic(json_chunk_type, 0x4E, 0x4F, 0x53, 0x4A)) {
                  Debug.LogError("JSON Header Magic Number Not Match");
                }
                var json_length = ReadUInt32(json_chunk_length);
                var json_bytes = new Span<byte>(new byte[json_length]);
                stream.Read(json_bytes);


                stream.Read(bin_chunk_length);
                stream.Read(bin_chunk_type);
                if(!ValidateMagic(bin_chunk_type, 0x00, 0x4E, 0x49, 0x42)) {
                  Debug.LogError("Bin Header Magic Number Not Match");
                }
                var bin_length = (int) ReadUInt32(bin_chunk_length);
                var bin_data = new Span<byte>(new byte[bin_length]);
                stream.Read(bin_data);
                List<byte[]> binData = new List<byte[]>
                {
                    bin_data.ToArray()
                };

                var gameObjectMap = new Dictionary<GameObject,int>();
                MapGameObjects(gameObjects, gameObjectMap, gameObjectExportSettings, settings);
                var new_json = ModifyJSON(gameObjectMap, json_bytes, binData, bin_length);

                bin_length = 0;
                foreach(byte[] arr in binData) {
                  bin_length += arr.Length;
                }
                int extraBytes = 4 - (bin_length % 4);
                if(extraBytes > 0) {
                  binData.Add(new byte[extraBytes]);
                  bin_length += extraBytes;
                }

                var outStream = new FileStream(path, FileMode.Create);
                outStream.Write(header_magic);
                outStream.Write(header_version);

                int totalLength = 12 + 8 + new_json.Length + 8 + bin_length;
                outStream.Write(WriteUInt32(totalLength));
                outStream.Write(WriteUInt32(new_json.Length));
                outStream.Write(json_chunk_type);
                outStream.Write(new_json);
                outStream.Write(WriteUInt32(bin_length));
                outStream.Write(bin_chunk_type);
                foreach(byte[] arr in binData) {
                  outStream.Write(arr, 0, arr.Length);
                }
                outStream.Close();
            }
        }
}

#endif
