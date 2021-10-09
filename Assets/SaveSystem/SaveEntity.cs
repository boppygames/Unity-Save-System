#define SAVE_DEBUG

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;

namespace EntitySaveSystem
{
  public interface ISavePropertyMissing
  {
    /// <summary>
    /// This callback is invoked when a field is available in a save file but the behaviour is now missing that
    /// field.
    /// </summary>
    /// <param name="name">Name of the field</param>
    /// <param name="reader">A reader that can be used to load the property.</param>
    void OnMissingProperty(string name, EntityReader reader);
  }

  public interface IEntityLoadComplete
  {
    /// <summary>
    /// This callback is invoked when this specific entity has completed loading. This can be used for post processing
    /// save data.
    /// </summary>
    void OnEntityLoadComplete();
  }

  public interface ILoadComplete
  {
    /// <summary>
    /// This callback is invoked when all entities in the save file have been loaded.
    /// </summary>
    void OnAllEntitiesLoaded();
  }

  public interface IBeforeEntitySave
  {
    /// <summary>
    /// This callback is invoked just before this entity is saved to disk.
    /// </summary>
    void OnBeforeSave();
  }

  [DisallowMultipleComponent]
  public class SaveEntity : MonoBehaviour
  {
    // This is a uuid that uniquely identifies this entity
    [Save] string entityId;

    // The UUID used to lookup this asset
    [SerializeField, HideInInspector] string serializedName;
    [SerializeField, HideInInspector] string assetId;

    void Start()
    {
      // Assign a new entityId, this may get overwritten later by Load()
      if (SaveSystem.instance == null) return;
      entityId = Guid.NewGuid().ToString();
      SaveSystem.instance.Register(this);
    }

    void OnDestroy()
    {
      SaveSystem.instance.Unregister(this);
    }

#if UNITY_EDITOR
    internal void EditorSetAssetId(string assetId) => this.assetId = assetId;
#endif

    internal string GetEntityId() => entityId;

    internal string GetAssetId() => assetId;

    internal string GetAssetName() => serializedName;

    internal void Save(EntityWriter writer)
    {
      var beforeSave = GetComponents<IBeforeEntitySave>();
      foreach (var invoke in beforeSave)
        invoke?.OnBeforeSave();

      foreach (var behaviour in GetComponents<MonoBehaviour>())
      {
        var index = SaveUtil.GetComponentIndex(this, behaviour.GetType(), behaviour);
        foreach (var saveField in GetFields(behaviour.GetType()))
        {
#if SAVE_DEBUG
          Debug.Log($"Saving field: {behaviour.GetType()}:{saveField.Name}");
#endif
          if (!writer.AddValue(behaviour, index, saveField, saveField.GetValue(behaviour)))
            Debug.LogError($"A field has failed to be written: {behaviour.GetType()} => {saveField.Name}");
        }
      }
    }

    internal void Load(EntityReader reader)
    {
      // Load values for each of our scripts
      foreach (var behaviour in GetComponents<MonoBehaviour>())
      {
        var index = SaveUtil.GetComponentIndex(this, behaviour.GetType(), behaviour);
        foreach (var loadField in GetFields(behaviour.GetType()))
        {
#if SAVE_DEBUG
          Debug.Log($"Loading field: {behaviour.GetType()}:{loadField.Name}");
#endif
          loadField.SetValue(behaviour, reader.Read(behaviour, index, loadField, loadField.GetValue(behaviour)));
        }
      }

      // Process any unread properties - these are properties that exist in the save file but were not loaded. This
      // specifically can be an issue with backwards compatibility, so we want to be able to address it.
      var unread = reader.GetUnreadProperties();
      foreach (var behaviour in GetComponents<MonoBehaviour>())
      {
        var missingInterface = behaviour.GetComponent<ISavePropertyMissing>();
        var index = SaveUtil.GetComponentIndex(this, behaviour.GetType(), behaviour);
        foreach (var unreadProperty in unread)
        {
          var split = unreadProperty.Split('.');
          var typeNameString = split[0];
          if (!int.TryParse(split[1], out var unreadIndex))
            continue;

#if SAVE_DEBUG
          Debug.LogWarning($"Missing receiver for property: {unreadProperty}");
#endif

          if (behaviour.GetType().Name != typeNameString || index != unreadIndex) continue;
          missingInterface?.OnMissingProperty(split[2], reader);
        }
      }

      // Invoke the callback for after load complete
      var afterLoad = GetComponents<IEntityLoadComplete>();
      foreach (var invoke in afterLoad)
        invoke?.OnEntityLoadComplete();
    }

    /// <summary>
    /// This is invoked by EntitySaveManager after all entities have finished loading.
    /// </summary>
    public void AllEntitiesLoaded()
    {
      var afterLoad = GetComponents<ILoadComplete>();
      foreach (var invoke in afterLoad)
        invoke?.OnAllEntitiesLoaded();
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

#if UNITY_EDITOR

    static bool printedLoadError;

    bool ValidateAssetID()
    {
      if (!SaveUtil.IsPrefab(gameObject)) return false;
      const string saveListPath = "Assets/SaveSystem/SaveList.asset";
      var list = AssetDatabase.LoadAssetAtPath<AssetSaveList>(saveListPath);
      if (list == null)
      {
        if (printedLoadError) return false;
        Debug.LogError("The save list could not be found at path: ");
        return false;
      }

      var assetId = list.GetAssetId(gameObject);
      if (string.IsNullOrEmpty(assetId))
      {
        Debug.LogWarning($"This save asset is not registered: {name}");
        if (!string.IsNullOrEmpty(this.assetId))
        {
          this.assetId = "";
          serializedName = name;
          return true;
        }

        return false;
      }

      if (assetId == this.assetId && serializedName == name) return false;
      this.assetId = assetId;
      serializedName = name;
      return true;
    }

    void OnValidate()
    {
      if (Application.isPlaying) return;
      ValidateAssetID();
    }

    [CustomEditor(typeof(SaveEntity))]
    public class CustomSaveEditor : Editor
    {
      public override void OnInspectorGUI()
      {
        base.OnInspectorGUI();

        var save = (SaveEntity) target;
        if (save.ValidateAssetID())
          EditorUtility.SetDirty(save);
        if (!string.IsNullOrEmpty(save.assetId))
          GUILayout.Label($"AssetID: {save.assetId}");

        if (SaveUtil.IsPrefab(save.gameObject))
          if (GUILayout.Button("Refresh"))
            if (save.ValidateAssetID())
              EditorUtility.SetDirty(save);
      }
    }
#endif
  }
}