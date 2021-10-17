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
using UnityEngine;
using FileMode = System.IO.FileMode;

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

    readonly Dictionary<string, SaveEntity> entities = new Dictionary<string, SaveEntity>();
    bool isLoading;
    uint saveSystemMagic = 0xB099001;

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
        writer.AddValue("refId", typeof(string), entity.GetEntityId());
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
      if (entities.TryGetValue(saveEntity.GetEntityId(), out var result))
      {
        if (saveEntity == result) return;
        Debug.LogError($"Tried to register different entity with same ID: " +
                       $"{saveEntity.name}:{saveEntity.GetEntityId()}");
        return;
      }

      entities.Add(saveEntity.GetEntityId(), saveEntity);
    }

    /// <summary>
    /// Removes an entity from the registry - this entity will no longer be saved when save is called.
    /// </summary>
    /// <param name="saveEntity">The entity to remove from the registry.</param>
    public void Unregister(SaveEntity saveEntity) => entities.Remove(saveEntity.GetEntityId());

    /// <summary>
    /// Returns a reference to a component on the specified entity with the given type and 
    /// </summary>
    /// <param name="entityId">The ID of the entity that you want to find</param>
    /// <param name="type">The type of the component that you want</param>
    /// <param name="compIndex">The index of the component on the entity</param>
    /// <returns></returns>
    public Component GetReference(string entityId, Type type, int compIndex)
    {
      if (!entities.TryGetValue(entityId, out var saveEntity)) return null;
      var comps = saveEntity.GetComponents(type);
      return compIndex < comps.Length && compIndex >= 0 ? comps[compIndex] : null;
    }

    /// <summary>
    /// Returns an entity reference as a GameObject.
    /// </summary>
    /// <param name="entityId">The entity ID of the entity you want a reference to</param>
    /// <returns></returns>
    public GameObject GetGOReference(string entityId) =>
      !entities.TryGetValue(entityId, out var saveEntity) ? null : saveEntity.gameObject;

    public bool SaveAllEntities(string fileName)
    {
      // A temporary buffer for entities - if an entity needs more than 32K it can grow.
      using var entityMemoryStream = new MemoryStream(32768);
      using var entityBinaryWriter = new BinaryWriter(entityMemoryStream);

      // Format Notes: This is what a save file looks like
      //  32bit magic int: for versioning
      //  32bit int: Entity count
      //  List of entities, 3 strings each. This is just a header.
      //  List of all all entity data, starting with a 32bit integer which states the size, then the buffer.

      try
      {
        if (File.Exists(fileName)) File.Delete(fileName);
        using var fileStream = new FileStream(fileName, FileMode.OpenOrCreate);
        using var fileBinaryWriter = new BinaryWriter(fileStream);

        // First thing we should do is write the magic
        fileBinaryWriter.Write(saveSystemMagic);

        // Write the count of entities (important for reading)
        fileBinaryWriter.Write(entities.Count);
        var entityWriter = new EntityWriter(entityBinaryWriter, customSerializers);

        // Write a lookup table of entities => prefabs
        entityMemoryStream.Position = 0;
        entityMemoryStream.SetLength(0);
        foreach (var entityEntry in entities)
        {
          var entityId = entityEntry.Key;
          var assetId = entityEntry.Value.GetAssetId();
          if (string.IsNullOrEmpty(entityId)) continue;
          if (string.IsNullOrEmpty(assetId)) continue;

          entityBinaryWriter.Write(entityId);
          entityBinaryWriter.Write(assetId);
          entityBinaryWriter.Write(entityEntry.Value.GetAssetName());
        }

        // Write the lookup table to the file
        entityMemoryStream.Position = 0;
        fileStream.Write(entityMemoryStream.GetBuffer(), 0, (int) entityMemoryStream.Length);
        entityMemoryStream.SetLength(0);

        // Write out the data for each entity
        foreach (var entityEntry in entities)
        {
          var entity = entityEntry.Value;
          var entityId = entity.GetEntityId();
          if (string.IsNullOrEmpty(entity.GetAssetId()))
          {
            Debug.LogError($"Entity has no ID, skipping: {entity.name}");
            continue;
          }

          entityMemoryStream.Position = 0;
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
          fileBinaryWriter.Write(entityId);
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

    /// <summary>
    /// Loads all entities in the specified save file.
    /// </summary>
    /// <param name="fileName">The save file to load</param>
    /// <param name="compressed">Whether or not the save file is compressed and needs to be decompressed.</param>
    /// <param name="destroyExisting">Whether or not to destroy existing scene objects before loading. If you load a file with entities that already exist in the scene, you may have unexpected behaviour</param>
    /// <exception cref="Exception"></exception>
    /// <exception cref="EndOfStreamException"></exception>
    public void LoadAllEntities(string fileName, bool destroyExisting = true)
    {
      if (entities.Count != 0 && entities.Values.Any(a => a != null))
      {
        if (destroyExisting)
        {
          foreach (var entity in entities) Destroy(entity.Value.gameObject);
          entities.Clear();
        }
        else
        {
          Debug.LogWarning("There are some entities in our entity table, it is recommended to destroy these"
                           + " before calling LoadAllEntities. This may cause issues.");
        }
      }

      try
      {
        isLoading = true;
        using var entityMemoryStream = new MemoryStream(32768);
        using var entityBinaryReader = new BinaryReader(entityMemoryStream);
        var entityReader = new EntityReader(entityBinaryReader, customDeserializers);

        try
        {
          using var fileStream = new FileStream(fileName, FileMode.OpenOrCreate);
          using var fileBinaryReader = new BinaryReader(fileStream);

          // Read and verify magic
          var magic = fileBinaryReader.ReadUInt32();
          if (magic != saveSystemMagic)
            throw new Exception($"File magic mismatch, got: {magic} expected: {saveSystemMagic}");

          // Dictionary from entityId => entity
          var loadedEntities = new Dictionary<string, SaveEntity>();
          var entityCount = fileBinaryReader.ReadInt32();

          // Read the entity table
          for (var x = 0; x < entityCount; x++)
          {
            var entityId = fileBinaryReader.ReadString();
            var assetId = fileBinaryReader.ReadString();
            var assetName = fileBinaryReader.ReadString();

            // Spawn this entity and set its entityId
            var assetPrefab = assetList.GetAssetById(assetId);
            if (assetPrefab == null)
            {
              Debug.LogError($"Cannot find prefab for asset {assetName}: {assetId}");
              continue;
            }

            var inst = Instantiate(assetPrefab);
            var saveEntity = inst.GetComponent<SaveEntity>();
            if (saveEntity == null)
            {
              Debug.LogWarning($"Entity no longer has SaveEntity, recovering! {assetName}");
              saveEntity = inst.AddComponent<SaveEntity>();
            }

            // Preload the info for this entity
            saveEntity.Preload(entityId, assetId, assetName);
            loadedEntities.Add(entityId, saveEntity);
            entities.Add(entityId, saveEntity);
          }

          // Read data for each entity
          for (var x = 0; x < entityCount; x++)
          {
            // Read the entityId
            var entityId = fileBinaryReader.ReadString();
            // Read the length of the data buffer
            var bufferSize = fileBinaryReader.ReadInt32();
            entityMemoryStream.SetLength(bufferSize);
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

            if (!loadedEntities.TryGetValue(entityId, out var saveEntity))
            {
              Debug.LogError($"Failed to find entity, skipping: {entityId}");
              continue;
            }

            entityMemoryStream.Position = 0;
            // Read all values from the buffer for this entity
            if (!entityReader.ReadAll())
            {
              Debug.LogError($"Deserialization failed for entity: {saveEntity.GetAssetName()}");
              continue;
            }

            try
            {
              // This is also very prone to crashing.
              saveEntity.Load(entityReader);
            }
            catch (Exception e)
            {
              Debug.LogError($"Failed to load entity: {saveEntity.GetAssetName()}:{saveEntity.GetAssetId()}");
              Debug.LogException(e);
              Destroy(saveEntity.gameObject);
            }
          }

          // Let all entities do their callbacks
          foreach (var entity in loadedEntities)
            entity.Value.AllEntitiesLoaded();
        }
        catch (Exception e)
        {
          Debug.LogException(e);
        }
      }
      finally
      {
        isLoading = false;
      }
    }
  }
}