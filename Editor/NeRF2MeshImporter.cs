using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using System.Collections;

public class NeRF2MeshImporter {

    private static readonly string FolderTitle = "Select folder with NeRF2Mesh Source Files";
    private static readonly string FolderExistsTitle = "Folder already exists";
    private static readonly string FolderExistsMsg = "A folder for this asset already exists in the Unity project. Overwrite?";
    private static readonly string OK = "OK";
    private static readonly string ImportErrorTitle = "Error importing NeRF2Mesh assets";

    [MenuItem("NeRF2Mesh/Import from disk", false, 0)]
    public static void ImportAssetsFromDisk() {
        // select folder with custom data
        string path = EditorUtility.OpenFolderPanel(FolderTitle, "", "");
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) {
            return;
        }

        // ask whether to overwrite existing folder
        string objName = new DirectoryInfo(path).Name;
        if (Directory.Exists(GetBasePath(objName))) {
            if (!EditorUtility.DisplayDialog(FolderExistsTitle, FolderExistsMsg, OK)) {
                return;
            }
        }


        ImportCustomScene(path);
    }
#pragma warning restore CS4014

    private static string GetBasePath(string objName) {
        return $"Assets/NeRF2Mesh/{objName}";
    }

    private static string GetMLPAssetPath(string objName) {
        string path = $"{GetBasePath(objName)}/MLP/{objName}.asset";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        return path;
    }

    private static string GetFeatureTextureAssetPath(string objName, int i)
    {
        string path = $"{GetBasePath(objName)}/JPGs/feat{i}.jpg";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        return path;
    }

    private static string GetMLPTextureAssetPath(string objName, int i)
    {
        string path = $"{GetBasePath(objName)}/MLP/feat{i}.jpg";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        return path;
    }

    private static string GetObjBaseAssetPath(string objName) {
        string path = $"{GetBasePath(objName)}/OBJs/";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        return path;
    }

    private static string GetObjAssetPath(string objName) {
        string path;

        path = $"{GetBasePath(objName)}/OBJs/shape.obj";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        return path;
    }

    private static string GetShaderAssetPath(string objName) {
        string path = $"{GetBasePath(objName)}/Shaders/{objName}_shader.shader";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        return path;
    }

    private static string GetDefaultMaterialAssetPath(string objName) {
        string path;

        path = $"{GetBasePath(objName)}/OBJs/Materials/shape-defaultMat.mat";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        return path;
    }

    public static void CreateNetworkWeightTexture(string objName, double[][] networkWeights, int id)
    {
        int width = networkWeights.Length;      // 32
        int height = networkWeights[0].Length;  // 3

        float[] weightsData = new float[width * height];
        for (int co = 0; co < height; co++)
        {
            for (int ci = 0; ci < width; ci++)
            {
                int index = co * width + ci; // column-major
                double weight = networkWeights[ci][co];
                weightsData[index] = (float)weight;
            }
        }

        int widthPad = width + (4 - width % 4); // make divisible by 4
        float[] weightsDataPad = new float[widthPad * height];
        for (int j = 0; j < widthPad; j += 4)
        {
            for (int i = 0; i < height; i++)
            {
                for (int c = 0; c < 4; c++)
                {
                    if (c + j >= width)
                    {
                        weightsDataPad[j * height + i * 4 + c] = 0.0f; // zero padding
                    }
                    else
                    {
                        weightsDataPad[j * height + i * 4 + c] = weightsData[j + i * width + c];
                    }
                }
            }
        }

        Texture2D texture = new Texture2D(1, widthPad * height / 4, TextureFormat.RFloat, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Unity.Collections.NativeArray<float> nativeArray = new Unity.Collections.NativeArray<float>(weightsDataPad, Unity.Collections.Allocator.Persistent);

        texture.LoadRawTextureData<float>(nativeArray);
        texture.Apply();

        byte[] textureBytes = texture.EncodeToPNG();
        string path = GetMLPTextureAssetPath(objName, id);
        File.WriteAllBytes(path, textureBytes);
        AssetDatabase.Refresh();

    }
    private static string GetPrefabAssetPath(string objName) {
        string path = $"{GetBasePath(objName)}/{objName}.prefab";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        return path;
    }
    /// <summary>
    /// Creates Unity assets for the given NeRF2Mesh assets on disk.
    /// </summary>
    /// <param name="path">The path to the folder with the NeRF2Mesh assets (OBJs, PNGs, mlp.json)</param>
    private static void ImportCustomScene(string path) {
        string objName = new DirectoryInfo(path).Name;

        Mlp mlp = CopyMLPFromPath(path);

        CreateNetworkWeightTexture(objName, mlp._0Weights, 0);
        CreateNetworkWeightTexture(objName, mlp._1Weights, 1);

        if (mlp == null) {
            return;
        }
        if (!CopyJPGsFromPath(path, mlp)) {
            return;
        }
        if (!CopyOBJsFromPath(path, mlp)) {
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ProcessAssets(objName);
    }



   


    /// <summary>
    /// Set specific import settings on OBJs/PNGs.
    /// Creates Materials and Shader from MLP data.
    /// Creates a convenient prefab for the NeRF2Mesh object.
    /// </summary>
    private static void ProcessAssets(string objName) {
        Mlp mlp = GetMlp(objName);
        CreateShader(objName, mlp);
        // PNGs are configured in PNGImportProcessor.cs
        ProcessOBJs(objName, mlp);
        CreatePrefab(objName, mlp);
    }

    /// <summary>
    /// Looks for a mlp.json at <paramref name="path"/> and imports it.
    /// </summary>
    private static Mlp CopyMLPFromPath(string path) {
        string objName = new DirectoryInfo(path).Name;

        string[] mlpPaths = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);
        if (mlpPaths.Length > 1) {
            EditorUtility.DisplayDialog(ImportErrorTitle, "Multiple mlp.json files found", OK);
            return null;
        }
        if (mlpPaths.Length <= 0) {
            EditorUtility.DisplayDialog(ImportErrorTitle, "No mlp.json files found", OK);
            return null;
        }

        string mlpJson = File.ReadAllText(mlpPaths[0]);
        TextAsset mlpJsonTextAsset = new TextAsset(mlpJson);
        AssetDatabase.CreateAsset(mlpJsonTextAsset, GetMLPAssetPath(objName));
        Mlp mlp = JsonConvert.DeserializeObject<Mlp>(mlpJson);
        return mlp;
    }

    private static Mlp GetMlp(string objName) {
        string mlpAssetPath = GetMLPAssetPath(objName);
        string mlpJson = AssetDatabase.LoadAssetAtPath<TextAsset>(mlpAssetPath).text;
        return JsonConvert.DeserializeObject<Mlp>(mlpJson);
    }

    /// <summary>
    /// Looks for and imports all feature textures for a given NeRF2Mesh scene.
    /// </summary>
    private static bool CopyJPGsFromPath(string path, Mlp mlp) {
        string objName = new DirectoryInfo(path).Name;
        
        string[] jpgPaths = Directory.GetFiles(path, "feat*.jpg", SearchOption.TopDirectoryOnly);

        for (int i=0; i<2; i++)
        {
            string featPath = jpgPaths[i]; // Path.Combine(path, $"shape{i}.pngfeat{j}.png");
            string featAssetPath = GetFeatureTextureAssetPath(objName, i);

            try
            {
                File.Copy(featPath, featAssetPath, overwrite: true);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        return true;
    }


    /// <summary>
    /// Looks for and imports all 3D models for a given NeRF2Mesh scene.
    /// </summary>
    private static bool CopyOBJsFromPath(string path, Mlp mlp) {
        string objName = new DirectoryInfo(path).Name;

        string[] objPaths = Directory.GetFiles(path, "mesh_*.obj", SearchOption.TopDirectoryOnly);
        
        for (int i=0; i<1; i++)
        {
            string objPath = objPaths[i];
            string objAssetPath = Path.Combine(GetObjBaseAssetPath(objName), $"shape.obj");

            try
            {
                File.Copy(objPath, objAssetPath, overwrite: true);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        return true;
    }

    private static bool AreOBJsSplit(string path) {
        if (Directory.GetFiles(path, "shape*_*.obj", SearchOption.TopDirectoryOnly).Length > 0) {
            return true;
        } else if (Directory.GetFiles(path, "shape*.obj", SearchOption.TopDirectoryOnly).Length > 0) {
            return false;
        } else {
            return false;
        }
    }

    private static int GetNumSplitShapes(bool splitShapes) {
        return splitShapes ? 8 : 1;
    }


    private static void ProcessOBJs(string objName, Mlp mlp) {
        bool splitShapes = AreOBJsSplit(GetObjBaseAssetPath(objName));
        int numSplitShapes = GetNumSplitShapes(splitShapes);
        string objAssetPath = GetObjAssetPath(objName);

        // create material
        string shaderAssetPath = GetShaderAssetPath(objName);
        Shader NeRF2MeshShader = AssetDatabase.LoadAssetAtPath<Shader>(shaderAssetPath);
        string materialAssetPath = GetDefaultMaterialAssetPath(objName);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
        material.shader = NeRF2MeshShader;

        // assign feature textures
        string feat0AssetPath = GetFeatureTextureAssetPath(objName, 0);
        string feat1AssetPath = GetFeatureTextureAssetPath(objName, 1);
        Texture2D featureTex1 = AssetDatabase.LoadAssetAtPath<Texture2D>(feat0AssetPath);
        Texture2D featureTex2 = AssetDatabase.LoadAssetAtPath<Texture2D>(feat1AssetPath);
        material.SetTexture("_MainTex", featureTex1);
        material.SetTexture("_SpecularTex", featureTex2);

        // assign mlp textures
        string mlpFeat0AssetPath = GetMLPTextureAssetPath(objName, 0);
        string mlpFeat1AssetPath = GetMLPTextureAssetPath(objName, 1);
        Texture2D mlpFeatureTex1 = AssetDatabase.LoadAssetAtPath<Texture2D>(mlpFeat0AssetPath);
        Texture2D mlpFeatureTex2 = AssetDatabase.LoadAssetAtPath<Texture2D>(mlpFeat1AssetPath);
        material.SetTexture("_MLP0", mlpFeatureTex1);
        material.SetTexture("_MLP1", mlpFeatureTex2);

        // assign material to renderer
        GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(objAssetPath);
        obj.GetComponentInChildren<MeshRenderer>().sharedMaterial = material;
    }

    /// <summary>
    /// Create shader and material for the specific object
    /// </summary>
    private static void CreateShader(string objName, Mlp mlp) {

        string shaderSource = ViewDependenceNetworkShader.Template;
        shaderSource = new Regex("OBJECT_NAME"       ).Replace(shaderSource, $"{objName}");


        for (int i = 0; i < mlp._0Weights.Length; i++) {
            StringBuilder first = toConstructorListFirst(mlp._0Weights[i]);
            StringBuilder last = toConstructorListLast(mlp._0Weights[i]);
            shaderSource = new Regex($"__W0_{i}__0").Replace(shaderSource, $"{first}");
            shaderSource = new Regex($"__W0_{i}__1").Replace(shaderSource, $"{last}");
        }
        for (int i = 0; i < mlp._1Weights.Length; i++) {
            shaderSource = new Regex($"__W1_{i}__").Replace(shaderSource, $"{toConstructorList(mlp._1Weights[i])}");
        }
        shaderSource = new Regex($"NUM_CHANNELS_ZERO").Replace(shaderSource, $"{mlp._0Weights.Length}");
        shaderSource = new Regex($"NUM_CHANNELS_ONE").Replace(shaderSource, $"{mlp._1Weights.Length}");
        shaderSource = new Regex($"NUM_CHANNELS_TWO").Replace(shaderSource, $"{mlp._1Weights[0].Length}");

        string shaderAssetPath = GetShaderAssetPath(objName);
        File.WriteAllText(shaderAssetPath, shaderSource);
        AssetDatabase.Refresh();
    }

    private static StringBuilder toConstructorList(double[] list)
    {
        System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.InvariantCulture;
        int width = list.Length;
        StringBuilder biasList = new StringBuilder(width * 12);
        for (int i = 0; i < width; i++)
        {
            double bias = list[i];
            biasList.Append(bias.ToString("F7", culture));
            if (i + 1 < width)
            {
                biasList.Append(", ");
            }
        }
        return biasList;
    }


    private static StringBuilder toConstructorListFirst(double[] list)
    {
        System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.InvariantCulture;
        int width = list.Length;
        StringBuilder biasList = new StringBuilder(width * 12);
        for (int i = 0; i < width /2 ; i++)
        {
            double bias = list[i];
            biasList.Append(bias.ToString("F7", culture));
            if (i + 1 < width / 2)
            {
                biasList.Append(", ");
            }
        }
        return biasList;
    }


    private static StringBuilder toConstructorListLast(double[] list)
    {
        System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.InvariantCulture;
        int width = list.Length;
        StringBuilder biasList = new StringBuilder(width * 12);
        for (int i = width / 2; i < width; i++)
        {
            double bias = list[i];
            biasList.Append(bias.ToString("F7", culture));
            if (i + 1 < width)
            {
                biasList.Append(", ");
            }
        }
        return biasList;
    }

    private static void CreatePrefab(string objName, Mlp mlp) {
        bool splitShapes = AreOBJsSplit(GetObjBaseAssetPath(objName));
        int numSplitShapes = GetNumSplitShapes(splitShapes);
        GameObject prefabObject = new GameObject(objName);
        for (int j = 0; j < numSplitShapes; j++) {
            GameObject shapeModel = AssetDatabase.LoadAssetAtPath<GameObject>(GetObjAssetPath(objName));
            GameObject shape = GameObject.Instantiate(shapeModel);
            shape.name = shape.name.Replace("(Clone)", "");
            shape.transform.SetParent(prefabObject.transform, false);
        }
        PrefabUtility.SaveAsPrefabAsset(prefabObject, GetPrefabAssetPath(objName));
        GameObject.DestroyImmediate(prefabObject);
    }


}