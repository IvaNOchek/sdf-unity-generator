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
    private bool m_generateCombinedSDF = false;
    private List<MeshFilter> m_meshFilters = new List<MeshFilter>();
    private List<Mesh> m_simpleMeshes = new List<Mesh>(); // Добавлено: список простых мешей
    private bool m_useTextureAtlas = false; // Добавлено: флаг для текстурного атласа
    private Vector2 scrollPos;

    [MenuItem("Tools/SDF Generator")]
    static void Init()
    {
        SdfGeneratorWindow window = (SdfGeneratorWindow)GetWindow(typeof(SdfGeneratorWindow));
        window.titleContent = new GUIContent("SDF Generator");
        window.Show();
    }

    public void OnGUI()
    {
        DrawHeader();
        DrawSaveLocation();
        DrawSettings();
        DrawMeshLists();
        DrawGenerateButton();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space();
        GUILayout.Label("SDF Generator", new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter });
        EditorGUILayout.Space();
    }

    private void DrawSaveLocation()
    {
        GUILayout.BeginHorizontal();
        GUI.enabled = false;
        EditorGUILayout.TextField("Save Location", m_savePath);
        GUI.enabled = true;
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            BrowseForSaveLocation();
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSettings()
    {
        EditorGUILayout.Space();
        m_sdfResolution = Convert.ToInt32((PowerOf8Size)EditorGUILayout.EnumPopup("SDF Resolution", (PowerOf8Size)m_sdfResolution));
        m_generateCombinedSDF = EditorGUILayout.Toggle("Generate Combined SDF", m_generateCombinedSDF);
        m_useTextureAtlas = EditorGUILayout.Toggle("Generate Texture Atlas", m_useTextureAtlas);
    }

    private void DrawMeshLists()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // MeshFilters
        EditorGUILayout.LabelField("Mesh Filters", EditorStyles.boldLabel);
        for (int i = 0; i < m_meshFilters.Count; i++)
        {
            DrawMeshFilterElement(i);
        }
        if (GUILayout.Button("Add Mesh Filter")) m_meshFilters.Add(null);

        EditorGUILayout.Space();

        // Simple Meshes
        EditorGUILayout.LabelField("Simple Meshes", EditorStyles.boldLabel);
        for (int i = 0; i < m_simpleMeshes.Count; i++)
        {
            DrawSimpleMeshElement(i);
        }
        if (GUILayout.Button("Add Simple Mesh")) m_simpleMeshes.Add(null);

        EditorGUILayout.EndScrollView();
    }

    private void DrawMeshFilterElement(int index)
    {
        GUILayout.BeginHorizontal();
        m_meshFilters[index] = (MeshFilter)EditorGUILayout.ObjectField($"Mesh Filter {index + 1}", m_meshFilters[index], typeof(MeshFilter), true);
        if (GUILayout.Button("Remove", GUILayout.Width(60)))
        {
            m_meshFilters.RemoveAt(index);
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSimpleMeshElement(int index)
    {
        GUILayout.BeginHorizontal();
        m_simpleMeshes[index] = (Mesh)EditorGUILayout.ObjectField($"Mesh {index + 1}", m_simpleMeshes[index], typeof(Mesh), false);
        if (GUILayout.Button("Remove", GUILayout.Width(60)))
        {
            m_simpleMeshes.RemoveAt(index);
        }
        GUILayout.EndHorizontal();
    }

    private void DrawGenerateButton()
    {
        EditorGUILayout.Space();
        GUI.enabled = HasValidMeshes();
        if (GUILayout.Button("Generate SDF"))
        {
            GenerateSDFs();
        }
        GUI.enabled = true;
    }

    private bool HasValidMeshes()
    {
        return (m_meshFilters.Any(mf => mf != null && mf.sharedMesh != null) ||
                m_simpleMeshes.Any(m => m != null));
    }

    private void GenerateSDFs()
    {
        if (m_useTextureAtlas)
        {
            GenerateTextureAtlas();
        }
        else if (m_generateCombinedSDF)
        {
            GenerateCombinedSDF();
        }
        else
        {
            GenerateIndividualSDFs();
        }
    }

    private void GenerateTextureAtlas()
    {
        // Собираем все меши
        List<Mesh> allMeshes = new List<Mesh>();
        allMeshes.AddRange(m_meshFilters.Where(mf => mf != null).Select(mf => mf.sharedMesh));
        allMeshes.AddRange(m_simpleMeshes.Where(m => m != null));

        // Генерируем атлас
        Texture2DArray textureAtlas = SDFTextureAtlasGenerator.GenerateAtlas(allMeshes, m_sdfResolution);
        SaveTextureAtlas(textureAtlas, "SDFTextureAtlas");
    }

    private void GenerateCombinedSDF()
    {
        Mesh combinedMesh = CombineAllMeshes();
        if (combinedMesh != null)
        {
            GenerateAndSaveSDF(combinedMesh, "CombinedSDF");
            DestroyImmediate(combinedMesh);
        }
    }

    private void GenerateIndividualSDFs()
    {
        // Создаем список для отслеживания всех сгенерированных текстур
        List<(Texture3D texture, string name)> generatedTextures = new List<(Texture3D, string)>();

        // Генерация для MeshFilters
        foreach (var meshFilter in m_meshFilters.Where(mf => mf != null && mf.sharedMesh != null))
        {
            try
            {
                Texture3D sdfTexture = SdfGenerator.GenerateSdf(meshFilter.sharedMesh, m_sdfResolution);
                generatedTextures.Add((sdfTexture, meshFilter.gameObject.name));
            }
            catch (Exception e)
            {
                Debug.LogError($"Error generating SDF for {meshFilter.gameObject.name}: {e.Message}");
            }
        }

        // Генерация для простых мешей
        foreach (var mesh in m_simpleMeshes.Where(m => m != null))
        {
            try
            {
                Texture3D sdfTexture = SdfGenerator.GenerateSdf(mesh, m_sdfResolution);
                generatedTextures.Add((sdfTexture, mesh.name));
            }
            catch (Exception e)
            {
                Debug.LogError($"Error generating SDF for {mesh.name}: {e.Message}");
            }
        }

        // Сохраняем все сгенерированные текстуры
        foreach (var (texture, name) in generatedTextures)
        {
            SaveSDFTexture(texture, name);
        }

        // Финальное обновление базы ассетов
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private Mesh CombineAllMeshes()
    {
        List<CombineInstance> combines = new List<CombineInstance>();

        // Добавляем меши из MeshFilters
        foreach (var mf in m_meshFilters.Where(mf => mf != null))
        {
            combines.Add(new CombineInstance
            {
                mesh = mf.sharedMesh,
                transform = mf.transform.localToWorldMatrix
            });
        }

        // Добавляем простые меши
        foreach (var mesh in m_simpleMeshes.Where(m => m != null))
        {
            combines.Add(new CombineInstance
            {
                mesh = mesh,
                transform = Matrix4x4.identity
            });
        }

        if (combines.Count == 0)
        {
            Debug.LogWarning("No valid meshes to combine.");
            return null;
        }

        Mesh combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(combines.ToArray(), true, true);
        return combinedMesh;
    }

    private void GenerateAndSaveSDF(Mesh mesh, string name)
    {
        if (mesh == null)
        {
            Debug.LogWarning($"Null mesh encountered for {name}. Skipping.");
            return;
        }

        try
        {
            Texture3D sdfTexture = SdfGenerator.GenerateSdf(mesh, m_sdfResolution);
            SaveSDFTexture(sdfTexture, name);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error generating SDF for {name}: {e.Message}");
        }
    }

    private void SaveSDFTexture(Texture3D texture, string name)
    {
        string sanitizedName = SanitizeFileName(name);
        string fileName = $"{sanitizedName}_SDF_{m_sdfResolution}";
        string assetPath = Path.Combine(m_savePath, fileName + ".asset");
        assetPath = assetPath.Replace("\\", "/");

        // Создаем уникальное имя файла
        assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

        // Настройка параметров текстуры перед сохранением
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        // Сохраняем текстуру как asset
        AssetDatabase.CreateAsset(texture, assetPath);

        // Применяем изменения
        EditorUtility.SetDirty(texture);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Настраиваем импортер текстуры
        var importer = AssetImporter.GetAtPath(assetPath) as AssetImporter;
        if (importer != null)
        {
            SerializedObject serializedImporter = new SerializedObject(importer);
            serializedImporter.ApplyModifiedProperties();
            importer.SaveAndReimport();
        }

        Debug.Log($"SDF saved: {assetPath}");
    }

    private void SaveTextureAtlas(Texture2DArray atlas, string name)
    {
        string fileName = $"{name}_{m_sdfResolution}";
        string assetPath = Path.Combine(m_savePath, fileName + ".asset");
        assetPath = assetPath.Replace("\\", "/");

        atlas.wrapMode = TextureWrapMode.Clamp;
        atlas.filterMode = FilterMode.Bilinear;

        assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
        AssetDatabase.CreateAsset(atlas, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Texture Atlas saved: {assetPath}");
    }

    private string SanitizeFileName(string fileName)
    {
        // Удаляем недопустимые символы из имени файла
        char[] invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }

    private void BrowseForSaveLocation()
    {
        string selectedPath = EditorUtility.OpenFolderPanel("Select Save Location", m_savePath, "");
        if (!string.IsNullOrEmpty(selectedPath))
        {
            if (selectedPath.StartsWith(Application.dataPath))
            {
                m_savePath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
            }
            else
            {
                EditorUtility.DisplayDialog("Invalid Path",
                    "Please select a folder inside the Assets directory.", "OK");
            }
        }
    }
}

// Вспомогательный класс для генерации текстурного атласа
public static class SDFTextureAtlasGenerator
{
    public static Texture2DArray GenerateAtlas(List<Mesh> meshes, int resolution)
    {
        if (meshes == null || meshes.Count == 0)
            throw new ArgumentException("No meshes provided for atlas generation");

        Texture2DArray atlas = new Texture2DArray(resolution, resolution, meshes.Count,
            TextureFormat.RFloat, false);

        for (int i = 0; i < meshes.Count; i++)
        {
            Texture3D sdf = SdfGenerator.GenerateSdf(meshes[i], resolution);
            CopySDFSliceToAtlas(sdf, atlas, i);
        }

        return atlas;
    }

    private static void CopySDFSliceToAtlas(Texture3D sdf, Texture2DArray atlas, int layer)
    {
        // Здесь должна быть реализация копирования центрального слоя SDF в атлас
        // Это зависит от конкретной реализации SdfGenerator
    }
}