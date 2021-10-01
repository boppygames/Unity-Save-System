using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor.EventSystems;
using UnityEngine;
using UnityEngine.Assertions;

public class EntityReader
{
  readonly BinaryReader reader;

  Dictionary<string, object> objects;

  readonly Dictionary<Type, Func<object, object>> reboxers = new Dictionary<Type, Func<object, object>>();

  public EntityReader(BinaryReader reader)
  {
    this.reader = reader;

    reboxers.Add(typeof(bool), a => a);
    reboxers.Add(typeof(char), a => ((string)a)[0]);
    reboxers.Add(typeof(byte), a => (byte) (long) a);
    reboxers.Add(typeof(sbyte), a => (sbyte)(long) a);
    reboxers.Add(typeof(short), a => (short) (long) a);
    reboxers.Add(typeof(ushort), a => (ushort) (long) a);
    reboxers.Add(typeof(uint), a => (uint) (long) a);
    reboxers.Add(typeof(int), a => (int) (long) a);
    reboxers.Add(typeof(long), a => a);
    reboxers.Add(typeof(ulong), a => (ulong) a);
    reboxers.Add(typeof(float), a => (float) (double) a);
    reboxers.Add(typeof(double), a => a);
  }

  internal object Read(FieldInfo typeInfo) => Read(typeInfo.FieldType, typeInfo.Name);

  object Read(Type type, string key)
  {
    if (SaveUtil.IsReferenceType(type))
    {
      if (!objects.TryGetValue(key, out var refValue)) return null;
      if (string.IsNullOrEmpty((string)refValue)) return null;
      var componentIndex = (int)(long) objects[$"{key}.compIndex"];
      return EntitySaveManager.instance.GetReference((string)refValue, type, componentIndex);
    } 
    
    if (type == typeof(GameObject))
    {
      if (!objects.TryGetValue(key, out var refValue)) return null;
      if (string.IsNullOrEmpty((string)refValue)) return null;
      return EntitySaveManager.instance.GetGOReference((string)refValue);
    }

    if(type.IsArray)
      return ReadArray(type, key);
    if (SaveUtil.IsIDictionaryType(type))
      return ReadDictionary(type, key);
    if (SaveUtil.IsIListType(type))
      return ReadList(type, key);
    if (type.IsEnum)
    {
      if (!objects.TryGetValue(key, out var value)) return null;
      return Enum.ToObject(type, (long)value);
    }
    
    if (SaveUtil.IsPrimitive(type))
    {
      // Hopefully this is a primitive
      if (!objects.TryGetValue(key, out var value)) return null;
      if (reboxers.TryGetValue(type, out var reboxer)) value = reboxer(value);
      return value;  
    }
    
    return ReadNonPrimitive(type, key);
  }
  
  /// <summary>
  /// Reads a non-primitive data type
  /// </summary>
  /// <param name="type">The type that you want to read</param>
  /// <param name="key">The key that was used to save the type</param>
  /// <returns>The deserialized data type</returns>
  object ReadNonPrimitive(Type type, string key)
  {
    var obj = Activator.CreateInstance(type);
    foreach (var field in SaveUtil.GetFields(type))
      field.SetValue(obj, Read(field.FieldType, $"{key}.{field.Name}"));

    return obj;
  }

  /// <summary>
  /// Reads an array with the given key
  /// </summary>
  /// <param name="key"></param>
  /// <param name="arrayType"></param>
  /// <returns></returns>
  object ReadArray(Type arrayType, string key)
  {
    if (!objects.TryGetValue($"{key}.Length", out var lengthValue)) return null;
    var arrayLength = (int) (long) lengthValue;
    var elementType = arrayType.GetElementType();
    Assert.IsTrue(arrayType.HasElementType && elementType != null);
    var arr = Array.CreateInstance(elementType, arrayLength);
    for (var x = 0; x < arrayLength; x++)
      arr.SetValue(Read(elementType, $"{key}.{x}"), x);
    return arr;
  }
  
  /// <summary>
  /// Reads a list object
  /// </summary>
  /// <param name="listType">The type of the list we are reading.</param>
  /// <param name="key">The key for this list</param>
  /// <returns></returns>
  object ReadList(Type listType, string key)
  {
    if (!objects.TryGetValue($"{key}.Count", out var countValue))
      return null;
    
    var count = (int) (long) countValue;
    var elementType = SaveUtil.GetIListType(listType);
    var list = Activator.CreateInstance(listType);
    // We have to lookup the Add method
    var addMethod = list.GetType().GetMethods()
      .First(m => m.Name == "Add"
                  && m.GetParameters().Length == 1
                  && m.GetParameters()[0].ParameterType == elementType);

    
    for (var x = 0; x < count; x++)
    {
      var entry = Read(elementType, $"{key}.{x}");
      addMethod.Invoke(list, new[] {entry});
    }

    return list;
  }
  
  object ReadDictionary(Type fieldType, string key)
  {
    var dictionaryTypes = SaveUtil.GetIDictionaryTypes(fieldType);
    var keyType = dictionaryTypes[0];
    var valueType = dictionaryTypes[1];

    var dictionary = Activator.CreateInstance(fieldType);
    // We have to lookup the Add method
    var addMethod = dictionary.GetType().GetMethods()
      .First(m => m.Name == "Add" 
                  && m.GetParameters().Length == 2 
                  && m.GetParameters()[0].ParameterType == keyType
                  && m.GetParameters()[1].ParameterType == valueType);

    var count = (int)(long)objects[$"{key}.Count"];
    for (var x = 0; x < count; x++)
    {
      var keyResult = Read(keyType, $"{key}.Keys.{x}");
      var valueResult = Read(valueType, $"{key}.Values.{x}");
      addMethod.Invoke(dictionary, new[] {keyResult, valueResult});
    }
    return dictionary;
  }

  public void ReadAll()
  {
    objects = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(reader.ReadString());
  }
}