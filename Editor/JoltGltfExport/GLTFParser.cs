#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

using Newtonsoft.Json.Linq;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;
using Codice.Client.BaseCommands;

public class GltfData {
  static uint HEADER_MAGIC = 0x46546C67;
  static uint HEADER_VERSION = 2;
  static uint JSON_CHUNK_TYPE = 0x4E4F534A;
  static uint BIN_CHUNK_TYPE = 0x004E4942;

  public JObject m_Json {get; set;}
  public List<byte[]> m_Bin {get; set;}


  private JArray m_Images {get; set;}
  private JArray m_BufferViews {get; set;}

  private Dictionary<int, int> m_TextureInstanceIds = new Dictionary<int, int>();

  static bool ValidateMagic(Span<byte> span, Span<byte> magic) {
    return span[0] == magic[0] && span[1] == magic[1] && span[2] == magic[2] && span[3] == magic[3];
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
  static Span<byte> WriteUInt32(uint val) {
    byte[] bytes = BitConverter.GetBytes(val);
    if (!BitConverter.IsLittleEndian)
      Array.Reverse(bytes);
    return new Span<byte>(bytes);
  }


  public static GltfData FromStream(Stream stream) {
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
    if(!ValidateMagic(header_magic, WriteUInt32(HEADER_MAGIC))) {
      Debug.LogError("GLTF Magic Mismatch");
    }
    if(!ValidateMagic(header_version, WriteUInt32(HEADER_VERSION))) {
      Debug.LogError("GLTF Version Not 2");
    }

    stream.Read(json_chunk_length);
    stream.Read(json_chunk_type);
    if(!ValidateMagic(json_chunk_type, WriteUInt32(JSON_CHUNK_TYPE))) {
      Debug.LogError("JSON Header Magic Number Not Match");
    }
    var json_length = ReadUInt32(json_chunk_length);
    var json_bytes = new Span<byte>(new byte[json_length]);
    stream.Read(json_bytes);

    stream.Read(bin_chunk_length);
    stream.Read(bin_chunk_type);
    if(!ValidateMagic(bin_chunk_type, WriteUInt32(BIN_CHUNK_TYPE))) {
      Debug.LogError("Bin Header Magic Number Not Match");
    }
    var bin_length = (int) ReadUInt32(bin_chunk_length);
    var bin_data = new Span<byte>(new byte[bin_length]);
    stream.Read(bin_data);
    List<byte[]> binData = new List<byte[]>
    {
        bin_data.ToArray()
    };
    var utf8 = new UTF8Encoding(); 
    var json_string = utf8.GetString(json_bytes.ToArray());
    JObject json = JObject.Parse(json_string);
    return new GltfData() {
      m_Json = json,
      m_Bin = binData,
      m_Images = (JArray) json["images"] ?? new JArray(),
      m_BufferViews = (JArray) json["bufferViews"] ?? new JArray()
    };
  }

  private int GetBufferLength() {
    int cur_bin_length = 0;
    int count = m_BufferViews.Count;
    if(count > 0) {
      var lastEntry = (JObject) m_BufferViews.Last;
      int byteLength = (int) lastEntry["byteLength"];
      int byteOffset = (int) lastEntry["byteOffset"];
      cur_bin_length = byteLength + byteOffset;
    }
    return cur_bin_length;
  }

  public int AddBufferView(byte[] binary) {
    int cur_bin_length = GetBufferLength();
    int count = m_BufferViews.Count;
    m_Bin.Add(binary);
    JObject bufferView = new JObject();
    bufferView["buffer"] = 0;
    bufferView["byteOffset"] = cur_bin_length;
    bufferView["byteLength"] = binary.Length;
    m_BufferViews.Add(bufferView);
    return count;
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


  public int AddImage(Texture2D texture, bool usePNG) {
    if(m_TextureInstanceIds.ContainsKey(texture.GetInstanceID())) {
      return m_TextureInstanceIds[texture.GetInstanceID()];
    }
    int imageId = m_TextureInstanceIds[texture.GetInstanceID()] = AddImage(EncodeTexture(texture, usePNG), usePNG);
    return imageId;
  }
  public int AddImage(byte[] texture, bool usePNG) {
    int count = m_Images.Count;
    int buffer_index = AddBufferView(texture);
    JObject image = new JObject();
    image["mimeType"] = usePNG ? "image/png" : "image/jpeg";
    image["bufferView"] = buffer_index;
    m_Images.Add(image);
    return count;
  }

  public void WriteToStream(Stream outStream) {

    int bin_length = 0;
    foreach(byte[] arr in m_Bin) {
      bin_length += arr.Length;
    }
    if(bin_length % 4 != 0) {
      int extraBytes = 4 - (bin_length % 4);
      m_Bin.Add(new byte[extraBytes]);
      bin_length += extraBytes;
    }

    if(m_BufferViews.Count > 0) {
      m_Json["bufferViews"] = m_BufferViews;
    }
    if(m_Images.Count > 0) {
      m_Json["images"] = m_Images;
    }

    if(bin_length > 0) {
      m_Json["buffers"][0]["byteLength"] = bin_length;
    }


    var utf8 = new UTF8Encoding();
    var json_string = m_Json.ToString(Newtonsoft.Json.Formatting.None);
    var length = json_string.Length;
    var json_padding = "";
    if(length % 4 != 0)
    for(var i=0;i< 4 - (length % 4);i++) {
      json_padding += " ";
    }
    json_string += json_padding;
    length = json_string.Length;

    var new_json = new byte[length];
    utf8.GetBytes(json_string, 0, length, new_json, 0);

    outStream.Write(WriteUInt32(HEADER_MAGIC));
    outStream.Write(WriteUInt32(HEADER_VERSION));

    int totalLength = 12 + 8 + new_json.Length + ((bin_length > 0) ? 8 + bin_length: 0);
    outStream.Write(WriteUInt32(totalLength));
    outStream.Write(WriteUInt32(new_json.Length));
    outStream.Write(WriteUInt32(JSON_CHUNK_TYPE));
    outStream.Write(new_json);
    if(bin_length > 0) {
      outStream.Write(WriteUInt32(bin_length));
      outStream.Write(WriteUInt32(BIN_CHUNK_TYPE));
      foreach(byte[] arr in m_Bin) {
        outStream.Write(arr, 0, arr.Length);
      }
    }
    outStream.Close();
  }
}

#endif