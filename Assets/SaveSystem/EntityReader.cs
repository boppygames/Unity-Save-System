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
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Assertions;

namespace EntitySaveSystem
{
  public class EntityReader
  {
    public class Adapter
    {
      readonly EntityReader reader;
      readonly string baseKey;

      public Adapter(EntityReader reader, string baseKey)
      {
        this.reader = reader;
        this.baseKey = baseKey;
      }

      public object Read(Type type, string name, object defaultValue = null)
      {
        if (string.IsNullOrEmpty(name))
          throw new ArgumentException("name must be set.");
        if (type == null)
          throw new ArgumentException("type must be set.");
        return reader.Read(type, $"{baseKey}.{name}", defaultValue);
      }
    }

    readonly BinaryReader reader;

    Dictionary<string, object> objects;
    HashSet<string> nullKeys;

    readonly Dictionary<Type, Func<object, object>> reboxers = new Dictionary<Type, Func<object, object>>();
    readonly Dictionary<Type, Func<Adapter, Type, object, object>> customDeserializers; 
    readonly List<string> readProperties = new List<string>();

    public EntityReader(BinaryReader reader, Dictionary<Type, Func<Adapter, Type, object, object>> customDeserializers)
    {
      this.reader = reader;
      this.customDeserializers = customDeserializers;

      reboxers.Add(typeof(bool), a => a);
      reboxers.Add(typeof(char), a => ((string) a)[0]);
      reboxers.Add(typeof(byte), a => (byte) (long) a);
      reboxers.Add(typeof(sbyte), a => (sbyte) (long) a);
      reboxers.Add(typeof(short), a => (short) (long) a);
      reboxers.Add(typeof(ushort), a => (ushort) (long) a);
      reboxers.Add(typeof(uint), a => (uint) (long) a);
      reboxers.Add(typeof(int), a => (int) (long) a);
      reboxers.Add(typeof(long), a => a);
      reboxers.Add(typeof(ulong), a => (ulong) a);
      reboxers.Add(typeof(float), a => (float) (double) a);
      reboxers.Add(typeof(double), a => a);
    }

    public string[] GetUnreadProperties() => objects.Keys.Except(readProperties).ToArray();

    internal object Read(Component comp, int index, FieldInfo typeInfo, object currentValue) =>
      Read(typeInfo.FieldType, $"{comp.GetType().Name}.{index}.{typeInfo.Name}", currentValue);

    object Read(Type type, string key, object defaultValue = null)
    {
      readProperties.Add(key);
      if (nullKeys.Contains(key)) return null;
      if (type.IsArray) return ReadArray(type, key, defaultValue);
      if (type.IsEnum)
      {
        if (!objects.TryGetValue(key, out var value)) return defaultValue;
        return Enum.ToObject(type, (long) value);
      }
      
      if (SaveUtil.IsPrimitive(type))
      {
        // Hopefully this is a primitive
        if (!objects.TryGetValue(key, out var value)) return defaultValue;
        if (reboxers.TryGetValue(type, out var reboxer)) value = reboxer(value);
        return value;
      }
      
      // Check to see if we have a serializer for this custom type
      var customType = type;
      while (customType != typeof(object) && customType != null)
      {
        if (customDeserializers.TryGetValue(customType, out var deserializer))
        {
          var adapter = new Adapter(this, key);
          return deserializer(adapter, type, defaultValue); 
        }

        customType = customType.BaseType;
      }
      
      if (SaveUtil.IsIDictionaryType(type))
        return ReadDictionary(type, key, defaultValue);
      if (SaveUtil.IsIListType(type))
        return ReadList(type, key, defaultValue);
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
    /// <param name="currentValue">The current value - this is typically returned when there is an issue.</param>
    /// <returns>The array as an object</returns>
    object ReadArray(Type arrayType, string key, object currentValue)
    {
      if (!objects.ContainsKey($"{key}.Length")) return currentValue;
      var arrayLength = (int) Read(typeof(int), $"{key}.Length");
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
    /// <param name="defaultValue">The default value that should be returned if an issue is encountered.</param>
    /// <returns>The list as an object</returns>
    object ReadList(Type listType, string key, object defaultValue)
    {
      if (!objects.ContainsKey($"{key}.Count")) return defaultValue;
      var count = (int) Read(typeof(int), $"{key}.Count");
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

    /// <summary>
    /// Reads a dictionary with the given defined Type fieldType.
    /// </summary>
    /// <param name="fieldType">The type of the dictionary</param>
    /// <param name="key">The key for looking up values</param>
    /// <param name="defaultValue">The default value to use if an issue is encountered</param>
    /// <returns></returns>
    object ReadDictionary(Type fieldType, string key, object defaultValue)
    {
      if (!objects.ContainsKey($"{key}.Count")) return defaultValue;

      var dictionaryTypes = SaveUtil.GetIDictionaryTypes(fieldType);
      var keyType = dictionaryTypes[0];
      var valueType = dictionaryTypes[1];

      var dictionary = defaultValue ?? Activator.CreateInstance(fieldType);
      // We have to lookup the Add method
      var addMethod = dictionary.GetType().GetMethods()
        .First(m => m.Name == "Add"
                    && m.GetParameters().Length == 2
                    && m.GetParameters()[0].ParameterType == keyType
                    && m.GetParameters()[1].ParameterType == valueType);

      var count = (int) Read(typeof(int), $"{key}.Count");
      for (var x = 0; x < count; x++)
      {
        var keyResult = Read(keyType, $"{key}.Keys.{x}");
        var valueResult = Read(valueType, $"{key}.Values.{x}");
        addMethod.Invoke(dictionary, new[] {keyResult, valueResult});
      }

      return dictionary;
    }

    public bool ReadAll()
    {
      objects = JsonConvert.DeserializeObject<Dictionary<string, object>>(reader.ReadString());
      nullKeys = JsonConvert.DeserializeObject<HashSet<string>>(reader.ReadString());
      return objects != null && nullKeys != null;
    }
  }
}
