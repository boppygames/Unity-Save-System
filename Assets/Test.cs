using System;
using System.Collections;
using System.Collections.Generic;
using EntitySaveSystem;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Assertions;

public class Test : MonoBehaviour, IEntityLoadComplete, IBeforeEntitySave
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

  [Save] Test otherTest;
  [SerializeField] [Save] List<MyData> data;
  [Save] int i;
  [Save] string s;

  [ContextMenu("Save")]
  public void Save()
  {
    SaveSystem.instance.SaveAllEntities("TestFile.dat");
  }
  
  [ContextMenu("Load")]
  public void Load()
  {
    SaveSystem.instance.LoadAllEntities("TestFile.dat");
  }

  public void OnEntityLoadComplete()
  {
    
  }

  public void OnBeforeSave()
  {
    
  }
}
