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
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EntitySaveSystem
{
  public class AssetSaveList : ScriptableObject
  {
    [Serializable]
    struct Entry
    {
      public string assetId;
      public GameObject asset;
    }

    [SerializeField] [HideInInspector] List<Entry> entries;

    public GameObject GetAssetById(string assetId)
    {
      foreach (var entry in entries)
        if (entry.assetId == assetId)
          return entry.asset;
      return null;
    }

    public GameObject GetAssetByName(string assetName)
    {
      foreach (var entry in entries)
        if (entry.asset.name == assetName)
          return entry.asset;
      return null;
    }

    public string GetAssetId(GameObject asset)
    {
      foreach (var entry in entries)
        if (entry.asset == asset)
          return entry.assetId;
      return null;
    }

#if UNITY_EDITOR

    [CustomEditor(typeof(AssetSaveList))]
    public class AssetSaveListEditor : Editor
    {
      GameObject selectedAsset;

      public override void OnInspectorGUI()
      {
        base.OnInspectorGUI();

        var list = (AssetSaveList) target;
        if (list.entries == null) return;

        for (var x = 0; x < list.entries.Count; x++)
        {
          var entry = list.entries[x];
          GUILayout.BeginHorizontal();

          if (GUILayout.Button("X"))
          {
            list.entries.RemoveAt(x);
            EditorUtility.SetDirty(list);
            return;
          }

          GUILayout.Label(entry.assetId);
          GUILayout.Label(entry.asset.name);

          GUILayout.EndHorizontal();
        }

        selectedAsset = EditorGUILayout.ObjectField("New Asset: ", selectedAsset,
          typeof(GameObject), false) as GameObject;

        var entity = selectedAsset != null ? selectedAsset.GetComponent<SaveEntity>() : null;

        if (list.GetAssetId(selectedAsset) != null)
        {
          GUILayout.Label("Warning: Asset already registered.");
        }
        else if (entity == null)
        {
          GUILayout.Label("Warning: Asset has no EntitySaveController.");
        }
        else if (GUILayout.Button("Add Asset"))
        {
          var entry = new Entry
          {
            asset = selectedAsset,
            assetId = Guid.NewGuid().ToString()
          };

          entity.EditorSetAssetId(entry.assetId);
          list.entries.Add(entry);
          selectedAsset = null;
          EditorUtility.SetDirty(list);
          EditorUtility.SetDirty(entity);
          return;
        }
      }
    }

#endif
  }
}
