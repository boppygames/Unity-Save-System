using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;

public class SaveUtil
{
  public static bool IsIListType(Type type)
  {
    foreach (var inter in type.GetInterfaces())
      if (inter.IsGenericType && inter.GetGenericTypeDefinition() == typeof(IList<>))
        return true;
    return false;
  }

  public static Type GetIListType(Type type)
  {
    foreach (var inter in type.GetInterfaces())
      if (inter.IsGenericType && inter.GetGenericTypeDefinition() == typeof(IList<>))
        return inter.GetGenericArguments().Single();
    return null;
  }
  
  public static bool IsIDictionaryType(Type type)
  {
    foreach (var inter in type.GetInterfaces())
      if (inter.IsGenericType && inter.GetGenericTypeDefinition() == typeof(IDictionary<,>))
        return true;
    return false;
  }

  public static Type[] GetIDictionaryTypes(Type type)
  {
    foreach (var inter in type.GetInterfaces())
      if (inter.IsGenericType && inter.GetGenericTypeDefinition() == typeof(IDictionary<,>))
        return inter.GetGenericArguments();
    return null;
  }

  public static bool IsPrimitive(Type t)
  {
    return t.IsPrimitive || t == typeof(string) || t.IsEnum;
  }

  public static IEnumerable<FieldInfo> GetFields(Type t)
  {
    return t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(
      a => a.IsPublic || a.CustomAttributes.Any(b => b.AttributeType == typeof(SaveAttribute)));
  }

  public static bool IsReferenceType(Type type)
  {
    while (type != typeof(object) && type != null)
    {
      if (type == typeof(Component))
        return true;
      type = type.BaseType;
    }
      
    return false;
  }
  
  public static int GetComponentIndex(EntitySaveController entity, Type componentType, object component)
  {
    var components = entity.GetComponents(componentType);
    for (var x = 0; x < components.Length; x++)
      if (ReferenceEquals(components[x], component))
        return x;
    return -1;
  }
}
