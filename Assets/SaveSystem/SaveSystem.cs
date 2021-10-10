// Copyright (c) 2021 Boppy Games
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.


using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.VersionControl;
using UnityEngine;
using FileMode = System.IO.FileMode;
using Object = UnityEngine.Object;

namespace EntitySaveSystem
{
  /// <summary>
  /// This is the singleton class for the save system.
  /// </summary>
  public class SaveSystem : MonoBehaviour
  {
    [SerializeField] bool dontDestroyOnLoad = true;
    [SerializeField] AssetSaveList assetList;

    public static SaveSystem instance;

    readonly List<SaveEntity> entities = new List<SaveEntity>();
    bool isLoading;

    protected readonly Dictionary<Type, Func<EntityReader.Adapter, Type, object, object>> customDeserializers =
      new Dictionary<Type, Func<EntityReader.Adapter, Type, object, object>>();

    protected readonly Dictionary<Type, Func<EntityWriter.Adapter, object, bool>> customSerializers =
      new Dictionary<Type, Func<EntityWriter.Adapter, object, bool>>();

    protected virtual void Awake()
    {
      if (instance != null)
      {
        Destroy(gameObject);
        return;
      }

      instance = this;
      if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
      AddCustomSerializers();
    }

    protected virtual void AddCustomSerializers()
    {
      customSerializers.Add(typeof(GameObject), (writer, value) =>
      {
        var go = (GameObject) value;
        var entity = go.GetComponent<SaveEntity>();
        if (entity == null) return false;
        writer.AddValue("entityRef", typeof(string), entity.GetEntityId());
        return true;
      });

      customSerializers.Add(typeof(Component), (writer, value) =>
      {
        var comp = (Component) value;
        var entity = comp.GetComponent<SaveEntity>();
        if (entity == null) return false;
        writer.AddValue("ref", typeof(string), entity.GetEntityId());
        writer.AddValue("compIndex", typeof(int),
          SaveUtil.GetComponentIndex(entity, value.GetType(), value));
        return true;
      });

      customDeserializers.Add(typeof(GameObject), (reader, fieldType, defaultValue) =>
      {
        var refId = reader.Read(typeof(string), "refId", null) as string;
        if (refId == null) return defaultValue;
        var result = instance.GetGOReference(refId);
        return result == null ? defaultValue : result;
      });

      customDeserializers.Add(typeof(Component), (reader, fieldType, defaultValue) =>
      {
        var refId = reader.Read(typeof(string), "refId") as string;
        if (string.IsNullOrEmpty(refId)) return defaultValue;
        var compIndexBoxed = reader.Read(typeof(int), "compIndex");
        if (compIndexBoxed == null) return defaultValue;
        var result = instance.GetReference(refId, fieldType, (int) compIndexBoxed);
        return result != null ? result : defaultValue;
      });
    }

    /// <summary>
    /// Whether or not a save file is currently being loaded. This helps entities decide whether or not to generate
    /// a new entityId.
    /// </summary>
    /// <returns>Whether or not a save file is currently being loaded</returns>
    public bool IsLoading() => isLoading;

    /// <summary>
    /// Returns the list of all assets that are saveable.
    /// </summary>
    /// <returns>The list of all assets that are saveable</returns>
    public AssetSaveList GetAssetSaveList() => assetList;

    /// <summary>
    /// Registers a new entity - this entity will be saved when save is called.
    /// </summary>
    /// <param name="saveEntity">The entity to register with this EntitySaveManager.</param>
    public void Register(SaveEntity saveEntity)
    {
      if (entities.Contains(saveEntity)) return;
      entities.Add(saveEntity);
    }

    /// <summary>
    /// Removes an entity from the registry - this entity will no longer be saved when save is called.
    /// </summary>
    /// <param name="saveEntity">The entity to remove from the registry.</param>
    public void Unregister(SaveEntity saveEntity) => entities.Remove(saveEntity);

    /// <summary>
    /// Returns a reference to a component on the specified entity with the given type and 
    /// </summary>
    /// <param name="entityId">The ID of the entity that you want to find</param>
    /// <param name="type">The type of the component that you want</param>
    /// <param name="compIndex">The index of the component on the entity</param>
    /// <returns></returns>
    public Component GetReference(string entityId, Type type, int compIndex)
    {
      var entity = entities.FirstOrDefault(a => a.GetEntityId() == entityId);
      if (entity == null) return null;
      var comps = entity.GetComponents(type);
      if (compIndex < comps.Length && compIndex >= 0)
        return comps[compIndex];
      return null;
    }

    /// <summary>
    /// Returns an entity reference as a GameObject.
    /// </summary>
    /// <param name="entityId">The entity ID of the entity you want a reference to</param>
    /// <returns></returns>
    public GameObject GetGOReference(string entityId)
    {
      var entity = entities.FirstOrDefault(a => a.GetEntityId() == entityId);
      if (entity == null) return null;
      return entity.gameObject;
    }

    public bool SaveAllEntities(string fileName)
    {
      // A temporary buffer for entities - if an entity needs more than 32K it can grow.
      using var entityMemoryStream = new MemoryStream(32768);
      using var entityBinaryWriter = new BinaryWriter(entityMemoryStream);

      try
      {
        if (File.Exists(fileName)) File.Delete(fileName);
        using var fileStream = new FileStream(fileName, FileMode.OpenOrCreate);
        using var fileBinaryWriter = new BinaryWriter(fileStream);

        // Write the count of entities (important for reading)
        fileBinaryWriter.Write(entities.Count);
        var entityWriter = new EntityWriter(entityBinaryWriter, customSerializers);

        foreach (var entity in entities)
        {
          if (string.IsNullOrEmpty(entity.GetAssetId()))
          {
            Debug.LogError($"Asset is not registered: {entity.name}");
            continue;
          }

          entityMemoryStream.Position = 0;
          entityBinaryWriter.Write(entity.GetAssetId());
          entityBinaryWriter.Write(entity.GetAssetName());
          try
          {
            // This is very prone to crashing, don't let one bad entity destroy the entire save file.
            entity.Save(entityWriter);
            entityWriter.WriteAll();
            Debug.Log($"Entity saved: {entity.name}");
          }
          catch (Exception e)
          {
            Debug.LogError($"Failure saving entity: {(entity != null ? entity.name : null)}");
            Debug.LogException(e);
            continue;
          }

          // This was a success, write the buffer to the file
          var expectedPosition = fileStream.Position + sizeof(int) + (int) entityMemoryStream.Position;
          fileBinaryWriter.Write((int) entityMemoryStream.Position);
          fileBinaryWriter.Write(entityMemoryStream.GetBuffer(), 0, (int) entityMemoryStream.Position);
          Debug.Assert(fileStream.Position == expectedPosition);
        }
      }
      catch (Exception e)
      {
        Debug.LogException(e);
        return false;
      }


      return true;
    }

    public void LoadAllEntities(string fileName)
    {
      try
      {
        isLoading = true;
        using var entityMemoryStream = new MemoryStream(32768);
        using var entityBinaryReader = new BinaryReader(entityMemoryStream);
        var entityReader = new EntityReader(entityBinaryReader);
        
        try
        {
          using var fileStream = new FileStream(fileName, FileMode.OpenOrCreate);
          using var fileBinaryReader = new BinaryReader(fileStream);

          var loadedEntities = new List<SaveEntity>();
          var entityCount = fileBinaryReader.ReadInt32();
          for (var x = 0; x < entityCount; x++)
          {
            
            var bufferSize = fileBinaryReader.ReadInt32();
            var readBytes = 0;

            try
            {
              while (readBytes != bufferSize)
              {
                var thisRead = fileBinaryReader.Read(entityMemoryStream.GetBuffer(), readBytes, 
                  bufferSize - readBytes);
                readBytes += thisRead;
                // Hit EOF
                if (thisRead == 0 && readBytes != bufferSize)
                  throw new EndOfStreamException();
              }
            }
            catch (Exception e)
            {
              Debug.LogError("Unexpected end of file!");
              Debug.LogException(e);
              break;
            }

            // read uuids for the entityId and assetId
            entityMemoryStream.Position = 0;
            var assetId = entityBinaryReader.ReadString();
            var assetName = entityBinaryReader.ReadString();
            
            // Try to get the asset or recover the 
            var asset = assetList.GetAssetById(assetId);
            if (asset == null)
            {
              Debug.LogError($"Asset is unavailable: {assetName} => {assetId}, we will try to recover.");
              asset = assetList.GetAssetByName(assetName);
              if (asset == null)
              {
                Debug.LogError($"Recovery failed for asset: {assetName}:{assetId} => no asset available. " +
                               "Make sure the asset is available in the asset list.");
                continue;
              }
            }

            // Read all values from the buffer for this entity
            if (!entityReader.ReadAll())
            {
              Debug.LogError($"Deserialization failed for entity: {assetName}");
              continue;
            }
            
            // Instantiate the asset and load its values
            var inst = Instantiate(asset);
            var saveController = inst.GetComponent<SaveEntity>();
            if (saveController == null)
            {
              Debug.LogError($"Entity is missing SaveEntity component! {assetName}");
              continue;
            }

            try
            {
              // This is also very prone to crashing.
              saveController.Load(entityReader);
            }
            catch (Exception e)
            {
              Debug.LogError($"Failed to load entity: {assetName}:{assetId}");
              Debug.LogException(e);
              Destroy(inst);
              continue;
            }
            
            loadedEntities.Add(saveController);
          }

          // Let all entities do their callbacks
          foreach (var entity in loadedEntities)
            entity.AllEntitiesLoaded();
        }
        catch (Exception e)
        {
          Debug.LogException(e);
          return;
        }
      }
      finally
      {
        isLoading = false;
      }
    }
  }
}
