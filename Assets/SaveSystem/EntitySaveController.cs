#define SAVE_DEBUG

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class EntitySaveController : MonoBehaviour
{
  // This is a uuid that uniquely identifies this entity
  [Save] string entityId;

  // The UUID used to lookup this asset
  [SerializeField] string assetId;

  void Start()
  {
    // Assign a new entityId, this may get overwritten later by Load()
    if (EntitySaveManager.instance == null) return;
    entityId = Guid.NewGuid().ToString();
    EntitySaveManager.instance.Register(this);
  }

  void OnDestroy()
  {
    EntitySaveManager.instance.Unregister(this);
  }

#if UNITY_EDITOR
  internal void EditorSetAssetId(string assetId) => this.assetId = assetId;
#endif

  internal string GetEntityId() => entityId;

  
  internal void Save(BinaryWriter writer)
  {
    var entityWriter = new EntityWriter(writer);

    foreach (var behaviour in GetComponents<MonoBehaviour>())
    {
      foreach (var saveField in GetFields(behaviour.GetType()))
      {
#if SAVE_DEBUG
        Debug.Log($"Saving field: {behaviour.GetType()}:{saveField.Name}");
#endif
        entityWriter.AddValue(saveField, saveField.GetValue(behaviour));
      }
    }

    // Write the results to the memory buffer
    entityWriter.WriteAll();
  }

  internal void Load(BinaryReader reader)
  {
    var entityReader = new EntityReader(reader);
    entityReader.ReadAll();

    foreach (var behaviour in GetComponents<MonoBehaviour>())
    {
      foreach (var loadField in GetFields(behaviour.GetType()))
      {
#if SAVE_DEBUG
        Debug.Log($"Loading field: {behaviour.GetType()}:{loadField.Name}");
#endif
        loadField.SetValue(behaviour, entityReader.Read(loadField));
      }
    }
  }

  List<FieldInfo> GetFields(Type type)
  {
    var fields = new List<FieldInfo>();
    foreach (var field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
    {
      // Skip any duplicate fields - this is not supported
      if (fields.Any(a => a.Name == field.Name))
      {
        Debug.LogWarning($"Multiple fields with same name {type}:{field.Name}");
        continue;
      }

      // Skip any fields that are not marked with save
      if (!Attribute.IsDefined(field, typeof(SaveAttribute))) continue;
      fields.Add(field);
    }

    // Get private fields within parent classes
    if (type.BaseType != null && type.BaseType != typeof(object))
    {
      foreach (var baseField in GetFields(type.BaseType).Where(
        a => fields.All(b => a.Name != b.Name)))
        fields.Add(baseField);
    }

    return fields;
  }
}