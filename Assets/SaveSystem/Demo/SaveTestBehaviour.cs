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
using UnityEngine;
using UnityEngine.Assertions;

namespace EntitySaveSystem
{
  public class SaveTestBehaviour : MonoBehaviour, IEntityLoadComplete, IBeforeEntitySave
  {
    [Serializable]
    public class MyData
    {
      public string s;
      public int a;
    }

    // A reference to another test behaviour in the scene
    [Save] public SaveTestBehaviour otherTest;
    [Save] public List<MyData> data = new List<MyData>();
    [Save] public int i;
    [Save] public string s;
    [Save] string nullTest;

    public void OnEntityLoadComplete()
    {
      if (data != null)
      {
        Debug.Log($"data: entries={data.Count}");
        foreach(var entry in data)
          Debug.Log($"\tEntry: {entry.s} {entry.a}");
      } else Debug.Log("Data was null");
      
      Debug.Log($"i: {i} s: {s}");
      Assert.IsTrue(nullTest == null);
    }

    public void OnBeforeSave()
    {
      nullTest = null;
    }
  }
}
