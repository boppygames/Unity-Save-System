using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using SaveSystem;
using UnityEngine;

public class EntityWriter
{
  readonly BinaryWriter writer;

  readonly Dictionary<string, object> objects = new Dictionary<string, object>();

  public EntityWriter(BinaryWriter writer)
  {
    this.writer = writer;
  }

  public void AddValue(Component comp, int index, FieldInfo fieldInfo, object value)
  {
    AddValue(fieldInfo.FieldType, $"{comp.GetType().Name}.{index}.{fieldInfo.Name}", value);
  }

  void AddValue(Type type, string key, object value)
  {
    if (value == null) return;
    if (key == null)
      throw new Exception("Key cannot be null");

    if (value is GameObject go)
    {
      var entity = go.GetComponent<EntitySaveController>();
      var refId = entity.GetEntityId();
      objects[key] = refId;
    }
    else if (value is Component comp)
    {
      var entity = comp.GetComponent<EntitySaveController>();
      var refId = entity.GetEntityId();
      objects[key] = refId;
      objects[$"{key}.compIndex"] = SaveUtil.GetComponentIndex(entity, type, value);
    }
    else if (type.IsArray)
      AddArray(key, value);
    else if (SaveUtil.IsIListType(type))
      WriteList(type, key, value);
    else if (SaveUtil.IsIDictionaryType(type))
      WriteDictionary(key, value);
    else if(SaveUtil.IsPrimitive(type))
      objects[key] = value;
    else WriteNonPrimitive(key, value);
  }
  
  void WriteNonPrimitive(string key, object value)
  {
    if (value == null) return;
    var type = value.GetType();
    foreach (var field in SaveUtil.GetFields(type))
      AddValue(field.FieldType, $"{key}.{field.Name}", field.GetValue(value));
  }

  void AddArray(string key, object value)
  {
    var array = (Array) value;
    var elementType = array.GetType().GetElementType();
    for (var index = 0; index < array.Length; index++)
      AddValue(elementType, $"{key}.{index}", array.GetValue(index));
    objects[$"{key}.Length"] = array.Length;
  }

  void WriteList(Type listType, string key, object value)
  {
    if (value == null) return;
    var getMethod = listType.GetMethod("get_Item");
    if (getMethod == null)
    {
      Debug.LogError($"Get method not found for list: given: {value.GetType()} expected: {listType}");
      return;
    }

    var countProperty = listType.GetProperty("Count");
    if (countProperty == null)
    {
      Debug.LogError($"Count property not found for list: given: {value.GetType()} expected: {listType}");
      return;
    }

    var elementType = SaveUtil.GetIListType(listType);
    var count = (int) countProperty.GetMethod.Invoke(value, new object[] { });

    for (var index = 0; index < count; index++)
      AddValue(elementType, $"{key}.{index}", getMethod.Invoke(value, new object[] {index}));
    AddValue(typeof(int), $"{key}.Count", count);
  }

  void WriteDictionary(string key, object value)
  {
    var index = 0;
    var dict = (IDictionary) value;
    var genericArguments = SaveUtil.GetIDictionaryTypes(dict.GetType());
    var keyType = genericArguments[0];
    var valueType = genericArguments[1];
    objects[$"{key}.Count"] = dict.Count;

    foreach (var dictKey in dict.Keys)
    {
      var dictValue = dict[dictKey];

      AddValue(keyType, $"{key}.Keys.{index}", dictKey);
      AddValue(valueType, $"{key}.Values.{index}", dictValue);
      index++;
    }
  }

  public void WriteAll()
  {
    var output = JsonConvert.SerializeObject(objects);
    writer.Write(output);
  }
}