using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public enum PowerOf8Size
{
    _16 = 16,
    _32 = 32,
    _64 = 64,
    _96 = 96,
    _128 = 128,
    _160 = 160,
    _200 = 200,
    _256 = 256
}

public class SdfGeneratorWindow : EditorWindow
{
    private string m_savePath
    {
        get => EditorPrefs.GetString("SdfGenerator_SavePath", "Assets");
        set => EditorPrefs.SetString("SdfGenerator_SavePath", value);
    }

    private int m_sdfResolution = 32;

    // Список Mesh для генерации SDF
    private List<Mesh> m_meshes = new List<Mesh>();

    // Для отображения списка в инспекторе
    private Vector2 scrollPos;

    [MenuItem("Utilities/SDF Generator")]
    static void Init()
    {
        SdfGeneratorWindow window = (SdfGeneratorWindow)EditorWindow.GetWindow(typeof(SdfGeneratorWindow));
        window.titleContent = new GUIContent("SDF Generator");
        window.Show();
    }

    public void OnGUI()
    {
        GUILayout.BeginVertical();
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // Заголовок
        EditorGUILayout.Space();
        GUILayout.Label("--- SDF Generator ---", new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        });
        EditorGUILayout.Space();

        // Выбор места сохранения для SDF
        GUILayout.BeginHorizontal();
        {
            GUI.enabled = false;
            EditorGUILayout.TextField("Save Location", m_savePath, GUILayout.MaxWidth(600));
            GUI.enabled = true;

            if (GUILayout.Button(EditorGUIUtility.IconContent("d_Folder Icon"), GUILayout.MaxWidth(40), GUILayout.MaxHeight(EditorGUIUtility.singleLineHeight)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("Select SDF Save Location", m_savePath, "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    if (selectedPath.StartsWith(Application.dataPath))
                    {
                        m_savePath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Invalid Path", "Please select a folder inside the Assets directory.", "OK");
                    }
                }
            }
        }
        GUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Выбор разрешения SDF
        m_sdfResolution = Convert.ToInt32((PowerOf8Size)EditorGUILayout.EnumPopup("SDF Resolution", (PowerOf8Size)m_sdfResolution));

        EditorGUILayout.Space();

        // Список Mesh
        GUILayout.Label("Meshes to Convert:", EditorStyles.boldLabel);
        for (int i = 0; i < m_meshes.Count; i++)
        {
            GUILayout.BeginHorizontal();
            m_meshes[i] = EditorGUILayout.ObjectField($"Mesh {i + 1}", m_meshes[i], typeof(Mesh), false) as Mesh;
            if (GUILayout.Button("Remove", GUILayout.MaxWidth(60)))
            {
                m_meshes.RemoveAt(i);
                i--;
            }
            GUILayout.EndHorizontal();
        }

        // Кнопка добавления нового Mesh
        if (GUILayout.Button("Add Mesh"))
        {
            m_meshes.Add(null);
        }

        EditorGUILayout.Space();

        // Кнопка генерации SDF
        GUI.enabled = m_meshes.Count > 0 && m_meshes.All(mesh => mesh != null);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Generate SDF", GUILayout.MaxWidth(150f)))
        {
            foreach (var mesh in m_meshes)
            {
                GenerateAndSaveSdf(mesh);
            }
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        GUI.enabled = true;

        EditorGUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    /// <summary>
    /// Генерация SDF и сохранение 3D текстуры для данного Mesh.
    /// </summary>
    /// <param name="mesh">Mesh для генерации SDF.</param>
    private void GenerateAndSaveSdf(Mesh mesh)
    {
        if (mesh == null)
        {
            Debug.LogWarning("Null mesh encountered. Skipping.");
            return;
        }

        // Генерация SDF
        Texture3D sdf = SdfGenerator.GenerateSdf(mesh, m_sdfResolution);

        // Сохранение 3D текстуры
        Save3DTexture(sdf, mesh.name);
    }

    /// <summary>
    /// Сохранение сгенерированной 3D текстуры SDF как Asset.
    /// </summary>
    /// <param name="texture">Сгенерированная 3D текстура.</param>
    /// <param name="meshName">Имя Mesh для использования в имени файла.</param>
    private void Save3DTexture(Texture3D texture, string meshName)
    {
        string assetPath = Path.Combine(m_savePath, $"{meshName}_SDF_{m_sdfResolution}.asset");
        assetPath = assetPath.Replace("\\", "/"); // Для кроссплатформенной совместимости

        // Проверка существования файла и создание уникального имени
        assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

        AssetDatabase.CreateAsset(texture, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"SDF for '{meshName}' saved at: {assetPath}");
    }
}