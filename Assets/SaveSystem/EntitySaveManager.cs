using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class EntitySaveManager : MonoBehaviour
{
  [SerializeField] AssetSaveList assetList;

  public static EntitySaveManager instance;

  readonly List<EntitySaveController> entities = new List<EntitySaveController>();
  bool isLoading;

  void Awake()
  {
    if (instance != null)
    {
      Destroy(gameObject);
      return;
    }

    instance = this;
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
  /// <param name="entity">The entity to register with this EntitySaveManager.</param>
  public void Register(EntitySaveController entity)
  {
    if (entities.Contains(entity)) return;
    entities.Add(entity);
  }

  /// <summary>
  /// Removes an entity from the registry - this entity will no longer be saved when save is called.
  /// </summary>
  /// <param name="entity">The entity to remove from the registry.</param>
  public void Unregister(EntitySaveController entity) => entities.Remove(entity);

  /// <summary>
  /// Returns a reference to a component on the specified entity with the given type and 
  /// </summary>
  /// <param name="entityId">The ID of the entity that you want to find</param>
  /// <param name="type">The type of the component that you want</param>
  /// <param name="compIndex">The index of the component on the entity</param>
  /// <returns></returns>
  public object GetReference(string entityId, Type type, int compIndex)
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
  /// <param name="entityId"></param>
  /// <returns></returns>
  public GameObject GetGOReference(string entityId)
  {
    var entity = entities.FirstOrDefault(a => a.GetEntityId() == entityId);
    if (entity == null) return null;
    return entity.gameObject;
  }

  public void SaveAllEntities(string fileName)
  {
    // using var entityMemoryStream = new MemoryStream(8192);
    // using var entityWriter = new BinaryWriter(entityMemoryStream);

    FileStream fileStream;

    try
    {
      File.Delete(fileName);
      fileStream = new FileStream(fileName, FileMode.OpenOrCreate);
    }
    catch (Exception e)
    {
      Debug.LogException(e);
      return;
    }

    using var fileWriter = new BinaryWriter(fileStream);

    // Write the count of entities (important for reading)
    fileWriter.Write(entities.Count);

    foreach (var entity in entities)
    {
      fileWriter.Write(entity.GetEntityId());
      fileWriter.Write(entity.GetAssetId());
      entity.Save(fileWriter);
    }

    fileStream.Dispose();
  }

  public void LoadAllEntities(string fileName)
  {
    isLoading = true;
    try
    {
      // using var entityMemoryStream = new MemoryStream(8192);
      // using var entityReader = new BinaryReader(entityMemoryStream);

      FileStream fileStream = null;

      try
      {
        fileStream = new FileStream(fileName, FileMode.OpenOrCreate);
      }
      catch (Exception e)
      {
        Debug.LogException(e);
        return;
      }

      using var fileReader = new BinaryReader(fileStream);

      var entityCount = fileReader.ReadInt32();
      for (var x = 0; x < entityCount; x++)
      {
        // read uuids for the entityId and assetId
        var assetId = fileReader.ReadString();
        var assetName = fileReader.ReadString();

        var asset = assetList.GetAsset(assetId);
        if (asset == null)
        {
          Debug.LogError($"Asset is unavailable: {assetName} => {assetId}");
          continue;
        }

        // Reset the stream position
        // entityMemoryStream.Position = 0;
        // var bufferLength = fileReader.ReadInt32();
        // entityMemoryStream.Write(fileReader.ReadBytes(bufferLength), 0, bufferLength);
        // entityMemoryStream.Position = 4;

        // Instantiate the asset and load its values
        var inst = Instantiate(asset);
        var saveController = inst.GetComponent<EntitySaveController>();
        saveController.Load(fileReader);
      }
    }
    finally
    {
      isLoading = false;
    }
  }
}