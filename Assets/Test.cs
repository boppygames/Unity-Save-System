using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Assertions;

public class Test : MonoBehaviour
{
  class MyData
  {
    public string s;
    public int a;
  }

  [SerializeField] [Save] List<MyData> data;

  [ContextMenu("Save")]
  public void Save()
  {
    data = new List<MyData>();
    data.Add(new MyData
    {
      s = "My string 1",
      a = 849375,
    });
    
    data.Add(new MyData
    {
      s = "My string 2",
      a = 3453,
    });
    
    data.Add(new MyData
    {
      s = "My string 3",
      a = 87972,
    });
    
    EntitySaveManager.instance.SaveAllEntities("TestFile.dat");
  }
  
  [ContextMenu("Load")]
  public void Load()
  {
    EntitySaveManager.instance.LoadAllEntities("TestFile.dat");
  }
}
