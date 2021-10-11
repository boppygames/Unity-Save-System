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
using EntitySaveSystem;
using UnityEngine;
using UnityEngine.Assertions;

public class DemoSaveSystem : SaveSystem
{
  public SaveTestBehaviour testPrefab1;
  public SaveTestBehaviour testPrefab2;

  void Start()
  {
    StartCoroutine(SaveTest());
  }

  IEnumerator SaveTest()
  {
    yield return new WaitForSeconds(1);
    
    // Instantiate one of each behaviour
    var instances = new[]
    {
      Instantiate(testPrefab1),
      Instantiate(testPrefab2),
    };

    // Update refs
    instances[0].otherTest = instances[1];
    instances[1].otherTest = instances[0];
    
    yield return new WaitForSeconds(1);
    
    // Save entities
    const string fileName = "MyTestFile.dat";
    Assert.IsTrue(SaveAllEntities(fileName));
    Debug.Log("Entities saved.");
    
    // Delete existing entities
    foreach(var entity in instances) Destroy(entity.gameObject);
    
    // Load entities
    LoadAllEntities(fileName);
    Debug.Log("Entities loaded.");
  }
}
