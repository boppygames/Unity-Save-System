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
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

namespace EntitySaveSystem
{
  public class EntityWriter
  {
    /// <summary>
    /// Adapters are useful for custom serialization callbacks. We don't want to give users direct access to the
    /// EntityWriter because users are idiots and will add values with the wrong keys and mess up the entire system.
    /// </summary>
    public class Adapter
    {
      readonly EntityWriter writer;
      readonly string baseKey;

      public Adapter(EntityWriter writer, string baseKey)
      {
        this.writer = writer;
        this.baseKey = baseKey;
      }

      /// <summary>
      /// Adds a value with the given name, type and value. This value may be null, however name and type must
      /// be set. Type must be either a parent type or the exact type of value (in the case where value is set).
      /// </summary>
      /// <param name="name">The name of this value. This would be the name of a field in a class/struct.</param>
      /// <param name="type">The type this value should be serialized as. This doesn't have to be the same as the expected type.</param>
      /// <param name="value">The value you want to write.</param>
      /// <exception cref="ArgumentException"></exception>
      public void AddValue(string name, Type type, object value)
      {
        if (string.IsNullOrEmpty(name))
          throw new ArgumentException("name must be set.");
        writer.AddValue(type, $"{baseKey}.{name}", value);
      }
    }

    // The writer that should be used to write to the underlying stream. This is typically a memory stream.
    readonly BinaryWriter writer;

    // The list of objects that have non-null values
    readonly Dictionary<string, object> objects = new Dictionary<string, object>();

    // The list of keys that contain null values
    readonly HashSet<string> nullKeys = new HashSet<string>();

    readonly Dictionary<Type, Func<Adapter, object, bool>> customSerializers;

    public EntityWriter(BinaryWriter writer, Dictionary<Type, Func<Adapter, object, bool>> customSerializers)
    {
      this.writer = writer;
      this.customSerializers = customSerializers;
    }

    public Adapter GetAdapter(string key) => new Adapter(this, key);

    public bool AddValue(Component comp, int index, FieldInfo fieldInfo, object value) =>
      AddValue(fieldInfo.FieldType, $"{comp.GetType().Name}.{index}.{fieldInfo.Name}", value);

    bool AddValue(Type type, string key, object value)
    {
      if (type == null)
        throw new Exception("Type cannot be null");
      if (key == null)
        throw new Exception("Key cannot be null");
      if (nullKeys.Contains(key))
        throw new ArgumentException($"This key is already registered as null: {key}");
      if (objects.ContainsKey(key))
        throw new ArgumentException($"This key is already registered as non-null: {key}");

      if (value == null)
      {
        nullKeys.Add(key);
        return true;
      }

      if (value is Component comp)
      {
        var entity = comp.GetComponent<SaveEntity>();
        if (entity == null) return false;
        var refId = entity.GetEntityId();
        objects[key] = refId;
        objects[$"{key}.compIndex"] = SaveUtil.GetComponentIndex(entity, type, value);
        return true;
      }

      if (type.IsArray) return AddArray(key, value);
      if (SaveUtil.IsPrimitive(type))
      {
        objects[key] = value;
        return true;
      }

      // Check to see if we have a serializer for this custom type
      var customType = type;
      while (customType != typeof(object) && customType != null)
      {
        if (customSerializers.TryGetValue(customType, out var serializer))
        {
          var adapter = new Adapter(this, key);
          if (serializer(adapter, value)) return true;
        }

        customType = customType.BaseType;
      }

      if (SaveUtil.IsIListType(type)) return WriteList(type, key, value);
      if (SaveUtil.IsIDictionaryType(type)) return WriteDictionary(key, value);
      
      // Last option: use the default serializer to serialize this object
      return WriteNonPrimitive(key, value);
    }

    bool WriteNonPrimitive(string key, object value)
    {
      var type = value.GetType();
      foreach (var field in SaveUtil.GetFields(type))
        AddValue(field.FieldType, $"{key}.{field.Name}", field.GetValue(value));
      return true;
    }

    bool AddArray(string key, object value)
    {
      var array = (Array) value;
      var elementType = array.GetType().GetElementType();
      for (var index = 0; index < array.Length; index++)
        AddValue(elementType, $"{key}.{index}", array.GetValue(index));
      objects[$"{key}.Length"] = array.Length;
      return true;
    }

    bool WriteList(Type listType, string key, object value)
    {
      var getMethod = listType.GetMethod("get_Item");
      if (getMethod == null)
      {
        Debug.LogError($"Get method not found for list: given: {value.GetType()} expected: {listType}");
        return false;
      }

      var countProperty = listType.GetProperty("Count");
      if (countProperty == null)
      {
        Debug.LogError($"Count property not found for list: given: {value.GetType()} expected: {listType}");
        return false;
      }

      var elementType = SaveUtil.GetIListType(listType);
      var count = (int) countProperty.GetMethod.Invoke(value, new object[] { });

      for (var index = 0; index < count; index++)
        AddValue(elementType, $"{key}.{index}", getMethod.Invoke(value, new object[] {index}));
      AddValue(typeof(int), $"{key}.Count", count);
      return true;
    }

    bool WriteDictionary(string key, object value)
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

      return true;
    }

    public void WriteAll()
    {
      var output = JsonConvert.SerializeObject(objects);
      writer.Write(output);
    }
  }
}
