using System;
using System.Collections;
using System.Collections.Generic;
using SaveSystem;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Assertions;

public class Test : MonoBehaviour
{
  [Serializable]
  class MyData
  {
    public string s;
    public int a;
  }

  class MyCustomStruct
  {
    public string myS;
    public int myI;
  }

  [SerializeField] [Save] List<MyData> data;
  [Save] int i;
  [Save] string s;

  void Start()
  {
    // EntitySaveManager.instance.AddCustomSerializer(typeof(MyCustomStruct),
    //   (writer, obj) =>
    //   {
    //     var data = obj as MyCustomStruct;
    //     writer
    //   }, reader =>
    //   {
    //     
    //     
    //     return null;
    //   });
  }

  [ContextMenu("Save")]
  public void Save()
  {
    EntitySaveManager.instance.SaveAllEntities("TestFile.dat");
  }
  
  [ContextMenu("Load")]
  public void Load()
  {
    EntitySaveManager.instance.LoadAllEntities("TestFile.dat");
  }
}
