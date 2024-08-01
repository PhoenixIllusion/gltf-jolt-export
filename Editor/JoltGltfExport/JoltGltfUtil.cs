#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Numerics;
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
using Object = UnityEngine.Object;
using static UnityGLTF.GLTFSceneExporter;

public class JoltGltfUtil {
  private GLTFSceneExporter exporter;
  private GLTFRoot root;
  private Dictionary<GameObject, Node> nodeMap;
  private Dictionary<int, ImageId> textures = new();

  public JoltGltfUtil( GLTFSceneExporter exporter, GLTFRoot root, Dictionary<GameObject, Node> nodeMap) {
    this.exporter = exporter;
    this.root = root;
    this.nodeMap = nodeMap;
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

  static byte[] EncodeTexture(Texture texture, bool usePNG)
  {
    RenderTexture destRenderTexture = RenderTexture.GetTemporary(
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

  static byte[] EncodeTexture(Texture2D texture, bool usePNG)
  {
    if(!texture.isReadable) {
      return EncodeTexture(texture as Texture, usePNG);
    }

    var imageData = usePNG ? texture.EncodeToPNG() : texture.EncodeToJPG(75);
    return imageData;
  }

  public int ExportRawImage(string name, byte[] imageBytes, string mimeType) {
      int hash = new BigInteger(imageBytes).GetHashCode();
			if (textures.TryGetValue(hash, out ImageId id))
			{
				return id.Id;
			}

      var image = new GLTFImage();
		  image.MimeType = mimeType;

      AccessorId accessorId = exporter.ExportAccessor(imageBytes);
      Accessor accessor = root.Accessors[accessorId.Id];
      root.Accessors.RemoveAt(accessorId.Id);
      image.BufferView = accessor.BufferView;
			image.Name = name;
      		   
      id = new ImageId
      {
        Id = root.Images.Count,
        Root = root
      };
      root.Images.Add(image);
      textures[hash] = id;

      return id.Id;
  }

  public int SetTextureRGB(RenderTexture texture, bool usePNG) {
    byte[] bytes = EncodeTexture(texture, usePNG);
    return ExportRawImage(texture.name, bytes, usePNG ? "image/png" : "image/jpeg");
  }

  public int SetTexture(Texture texture, bool usePNG) {
    byte[] bytes = EncodeTexture(texture, usePNG);
    return ExportRawImage(texture.name, bytes, usePNG ? "image/png" : "image/jpeg");
  }

  public int SetMesh(Mesh mesh) {
    MeshId id = exporter.ExportMesh(mesh);
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
