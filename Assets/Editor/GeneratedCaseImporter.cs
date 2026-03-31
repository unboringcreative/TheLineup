using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GeneratedCaseImporter
{
    private const string CasesRoot = "Assets/Scripts/Cases";
    private const string LastImportFolderEditorPrefKey = "TheLineup.LastGeneratedCaseImportFolder";

    [MenuItem("Tools/The Lineup/Import Case Folder...")]
    public static void ImportGeneratedCasesFromFolder()
    {
        string lastFolder = EditorPrefs.GetString(LastImportFolderEditorPrefKey, Directory.GetCurrentDirectory());
        string selectedAbsolute = EditorUtility.OpenFolderPanel(
            "Select Generated Case Folder or Root",
            lastFolder,
            string.Empty
        );

        if (string.IsNullOrWhiteSpace(selectedAbsolute))
        {
            Debug.Log("Case import cancelled.");
            return;
        }

        EditorPrefs.SetString(LastImportFolderEditorPrefKey, selectedAbsolute);
        ImportFromRootFolder(selectedAbsolute);
    }

    private static void ImportFromRootFolder(string rootFolderAbsolute)
    {
        if (!Directory.Exists(rootFolderAbsolute))
        {
            Debug.LogWarning($"Selected folder does not exist: {rootFolderAbsolute}");
            return;
        }

        List<string> candidateDirectories = ResolveImportDirectories(rootFolderAbsolute);
        if (candidateDirectories.Count == 0)
        {
            Debug.LogWarning($"No case folders with case.json were found under: {rootFolderAbsolute}");
            return;
        }

        int imported = 0;
        foreach (string caseDirectory in candidateDirectories)
        {
            if (ImportSingleCase(caseDirectory))
                imported++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log($"Generated case import complete. Imported {imported} case(s).");
    }

    private static List<string> ResolveImportDirectories(string rootFolderAbsolute)
    {
        List<string> results = new List<string>();

        string directCaseJson = Path.Combine(rootFolderAbsolute, "case.json");
        if (File.Exists(directCaseJson))
        {
            results.Add(rootFolderAbsolute);
            return results;
        }

        string[] caseJsonFiles = Directory.GetFiles(rootFolderAbsolute, "case.json", SearchOption.AllDirectories);
        HashSet<string> uniqueDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < caseJsonFiles.Length; i++)
        {
            string directory = Path.GetDirectoryName(caseJsonFiles[i]);
            if (!string.IsNullOrWhiteSpace(directory))
                uniqueDirectories.Add(directory);
        }

        results.AddRange(uniqueDirectories
            .Where(p => !IsIgnoredImportDirectory(p, rootFolderAbsolute))
            .OrderBy(p => p));

        return results;
    }

    private static bool IsIgnoredImportDirectory(string directoryAbsolute, string rootFolderAbsolute)
    {
        if (string.IsNullOrWhiteSpace(directoryAbsolute))
            return true;

        string folderName = Path.GetFileName(directoryAbsolute.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.Equals(folderName, "archive", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(folderName, "imported", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(folderName, "processed", StringComparison.OrdinalIgnoreCase))
            return true;

        string relative;
        try
        {
            relative = Path.GetRelativePath(rootFolderAbsolute, directoryAbsolute);
        }
        catch
        {
            relative = directoryAbsolute;
        }

        string normalized = relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        return normalized.StartsWith("archive/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("imported/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("processed/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ImportSingleCase(string caseDirectoryAbsolute)
    {
        string jsonPath = ResolveCaseJsonPath(caseDirectoryAbsolute);
        if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
        {
            Debug.LogWarning($"No usable case JSON found in {caseDirectoryAbsolute}");
            return false;
        }

        if (!TryLoadCaseJson(jsonPath, out GeneratedCaseData data, out string jsonError))
        {
            Debug.LogError($"Failed parsing case JSON: {jsonPath}\n{jsonError}");
            return false;
        }

        string validationError = ValidateCaseData(data);
        if (!string.IsNullOrEmpty(validationError))
        {
            Debug.LogError($"Invalid case JSON structure: {jsonPath}\n{validationError}");
            return false;
        }

        string caseFolderName = Sanitize(data.caseId);
        string caseAssetFolder = EnsureFolder(CasesRoot, caseFolderName);
        string imageFolder = EnsureFolder(caseAssetFolder, "Images");

        if (!CopyPngFiles(caseDirectoryAbsolute, imageFolder, out List<string> copiedAssetPaths))
        {
            Debug.LogError($"Import aborted for {data.caseId}. No PNG files were found in source folder: {caseDirectoryAbsolute}");
            return false;
        }

        for (int i = 0; i < copiedAssetPaths.Count; i++)
            AssetDatabase.ImportAsset(copiedAssetPaths[i], ImportAssetOptions.ForceSynchronousImport);

        EnsureSpritesInFolder(imageFolder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        SuspectProfileSO[] suspectAssets = new SuspectProfileSO[5];
        for (int i = 0; i < suspectAssets.Length; i++)
        {
            GeneratedSuspect s = i < data.suspects.Length ? data.suspects[i] : null;
            string suspectPath = $"{caseAssetFolder}/SuspectProfile {i + 1}.asset";
            SuspectProfileSO suspectAsset = LoadOrCreateAsset<SuspectProfileSO>(suspectPath);
            suspectAsset.displayName = s != null ? s.displayName : $"Suspect {i + 1}";
            suspectAsset.sex = s != null ? SafeString(s.sex) : string.Empty;
            suspectAsset.occupation = s != null ? SafeString(s.occupation) : string.Empty;
            suspectAsset.nationality = s != null ? SafeString(s.nationality) : string.Empty;
            suspectAsset.height = s != null ? SafeString(s.height) : string.Empty;
            suspectAsset.weight = s != null ? SafeString(s.weight) : string.Empty;
            suspectAsset.keyPersonalityTrait = s != null ? SafeString(s.keyPersonalityTrait) : string.Empty;
            suspectAsset.dialogue = s != null ? SafeString(s.dialogue) : string.Empty;

            string suspectStem = s != null ? ResolveGeneratedStem(s.portraitStem, "suspect", i + 1) : null;
            suspectAsset.portrait = s != null ? LoadSpriteByStem(imageFolder, suspectStem) : null;

            if (s != null && suspectAsset.portrait == null)
                Debug.LogWarning($"Missing suspect sprite for stem '{s.portraitStem}' (resolved '{suspectStem}') in {imageFolder}");

            EditorUtility.SetDirty(suspectAsset);
            suspectAssets[i] = suspectAsset;
        }

        EvidenceProfileSO[] evidenceAssets = new EvidenceProfileSO[3];
        for (int i = 0; i < evidenceAssets.Length; i++)
        {
            GeneratedEvidence e = i < data.evidence.Length ? data.evidence[i] : null;
            string evidencePath = $"{caseAssetFolder}/EvidenceProfile {i + 1}.asset";
            EvidenceProfileSO evidenceAsset = LoadOrCreateAsset<EvidenceProfileSO>(evidencePath);
            evidenceAsset.title = e != null ? e.title : $"Evidence {i + 1}";
            evidenceAsset.description = e != null ? SafeString(e.description) : string.Empty;
            evidenceAsset.discoveryLocation = e != null ? SafeString(e.discoveryLocation) : string.Empty;

            string evidenceStem = e != null ? ResolveGeneratedStem(e.imageStem, "evidence", i + 1) : null;
            evidenceAsset.image = e != null ? LoadSpriteByStem(imageFolder, evidenceStem) : null;

            if (e != null && evidenceAsset.image == null)
                Debug.LogWarning($"Missing evidence sprite for stem '{e.imageStem}' (resolved '{evidenceStem}') in {imageFolder}");

            EditorUtility.SetDirty(evidenceAsset);
            evidenceAssets[i] = evidenceAsset;
        }

        string caseDefinitionPath = $"{caseAssetFolder}/{caseFolderName}_Definition.asset";
        CaseDefinitionSO caseDefinition = LoadOrCreateAsset<CaseDefinitionSO>(caseDefinitionPath);
        caseDefinition.caseId = data.caseId;
        caseDefinition.caseTitle = data.caseTitle;
        caseDefinition.caseDescription = SafeString(data.caseDescription);
        caseDefinition.locationAddressOrBusiness = SafeString(data.location != null ? data.location.addressOrBusiness : data.locationAddressOrBusiness);
        caseDefinition.locationCity = SafeString(data.location != null ? data.location.city : data.locationCity);
        caseDefinition.locationCountry = SafeString(data.location != null ? data.location.country : data.locationCountry);

        string featuredStem = ResolveGeneratedStem(data.featuredImageStem, "featured", 1);
        caseDefinition.featuredImage = LoadSpriteByStem(imageFolder, featuredStem);
        if (caseDefinition.featuredImage == null)
            Debug.LogWarning($"Missing featured sprite for stem '{data.featuredImageStem}' (resolved '{featuredStem}') in {imageFolder}");

        caseDefinition.verdictTitle = string.IsNullOrWhiteSpace(data.verdictTitle) ? "Verdict" : data.verdictTitle;
        caseDefinition.explanation = SafeString(data.explanation);
        caseDefinition.suspects = suspectAssets;
        caseDefinition.evidence = evidenceAssets;
        caseDefinition.guiltySuspectIndex = Mathf.Clamp(data.guiltySlot - 1, 0, 4);
        EditorUtility.SetDirty(caseDefinition);

        AddCaseToLibrary(caseDefinition);
        return true;
    }

    private static bool TryLoadCaseJson(string jsonPath, out GeneratedCaseData data, out string error)
    {
        data = null;
        error = null;

        try
        {
            string json = File.ReadAllText(jsonPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON file is empty.";
                return false;
            }

            data = JsonUtility.FromJson<GeneratedCaseData>(json);
            if (data == null)
            {
                error = "JsonUtility returned null.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            return false;
        }
    }

    private static string ValidateCaseData(GeneratedCaseData data)
    {
        if (data == null)
            return "Case data is null.";

        if (string.IsNullOrWhiteSpace(data.caseId))
            return "Missing caseId.";

        if (string.IsNullOrWhiteSpace(data.caseTitle))
            return "Missing caseTitle.";

        if (string.IsNullOrWhiteSpace(data.caseDescription))
            return "Missing caseDescription.";

        if (data.location == null)
            return "Missing location object.";

        if (string.IsNullOrWhiteSpace(data.location.addressOrBusiness) ||
            string.IsNullOrWhiteSpace(data.location.city) ||
            string.IsNullOrWhiteSpace(data.location.country))
            return "Location must include addressOrBusiness, city, and country.";

        if (string.IsNullOrWhiteSpace(data.featuredImageStem))
            return "Missing featuredImageStem.";

        if (string.IsNullOrWhiteSpace(data.explanation))
            return "Missing explanation.";

        if (data.guiltySlot < 1 || data.guiltySlot > 5)
            return "guiltySlot must be between 1 and 5.";

        if (data.suspects == null)
            return "Missing suspects array.";

        if (data.suspects.Length != 5)
            return "Exactly 5 suspects are required.";

        if (data.evidence == null)
            return "Missing evidence array.";

        if (data.evidence.Length != 3)
            return "Exactly 3 evidence items are required.";

        for (int i = 0; i < data.suspects.Length; i++)
        {
            GeneratedSuspect suspect = data.suspects[i];
            if (suspect == null)
                return $"Suspect entry {i + 1} is null.";

            if (suspect.slot < 1 || suspect.slot > 5)
                return $"Suspect entry {i + 1} has invalid slot {suspect.slot}.";

            if (string.IsNullOrWhiteSpace(suspect.displayName) ||
                string.IsNullOrWhiteSpace(suspect.occupation) ||
                string.IsNullOrWhiteSpace(suspect.nationality) ||
                string.IsNullOrWhiteSpace(suspect.height) ||
                string.IsNullOrWhiteSpace(suspect.weight) ||
                string.IsNullOrWhiteSpace(suspect.keyPersonalityTrait) ||
                string.IsNullOrWhiteSpace(suspect.dialogue) ||
                string.IsNullOrWhiteSpace(suspect.portraitStem) ||
                string.IsNullOrWhiteSpace(suspect.portraitPromptBase))
                return $"Suspect entry {i + 1} is missing one or more required fields.";
        }

        for (int i = 0; i < data.evidence.Length; i++)
        {
            GeneratedEvidence item = data.evidence[i];
            if (item == null)
                return $"Evidence entry {i + 1} is null.";

            if (item.slot < 1 || item.slot > 3)
                return $"Evidence entry {i + 1} has invalid slot {item.slot}.";

            if (string.IsNullOrWhiteSpace(item.title) ||
                string.IsNullOrWhiteSpace(item.description) ||
                string.IsNullOrWhiteSpace(item.discoveryLocation) ||
                string.IsNullOrWhiteSpace(item.imageStem) ||
                string.IsNullOrWhiteSpace(item.imagePromptBase))
                return $"Evidence entry {i + 1} is missing one or more required fields.";
        }

        return null;
    }

    private static void AddCaseToLibrary(CaseDefinitionSO caseDefinition)
    {
        string[] guids = AssetDatabase.FindAssets("t:CaseLibrarySO");
        if (guids.Length == 0)
        {
            Debug.LogWarning("No CaseLibrarySO asset found. Skipping library update.");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        CaseLibrarySO library = AssetDatabase.LoadAssetAtPath<CaseLibrarySO>(path);
        if (library == null)
            return;

        if (library.cases == null)
            library.cases = new List<CaseDefinitionSO>();

        if (!library.cases.Contains(caseDefinition))
        {
            library.cases.Add(caseDefinition);
            EditorUtility.SetDirty(library);
        }
    }

    private static string ResolveCaseJsonPath(string caseDirectoryAbsolute)
    {
        string preferred = Path.Combine(caseDirectoryAbsolute, "case.json");
        if (File.Exists(preferred))
            return preferred;

        string[] jsonFiles = Directory.GetFiles(caseDirectoryAbsolute, "*.json", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < jsonFiles.Length; i++)
        {
            if (TryLoadCaseJson(jsonFiles[i], out GeneratedCaseData data, out _))
            {
                if (data != null && !string.IsNullOrWhiteSpace(data.caseId) && data.suspects != null && data.evidence != null)
                    return jsonFiles[i];
            }
        }

        return null;
    }

    private static bool CopyPngFiles(string sourceDirectoryAbsolute, string targetAssetFolder, out List<string> copiedAssetPaths)
    {
        copiedAssetPaths = new List<string>();

        string[] sourcePngs = Directory.GetFiles(sourceDirectoryAbsolute, "*.png", SearchOption.AllDirectories);
        if (sourcePngs.Length == 0)
        {
            Debug.LogError($"No PNG files found in source folder: {sourceDirectoryAbsolute}");
            return false;
        }

        string targetAbsolute = ToAbsolutePath(targetAssetFolder);
        Directory.CreateDirectory(targetAbsolute);

        string[] existingPngs = Directory.GetFiles(targetAbsolute, "*.png", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < existingPngs.Length; i++)
        {
            string pngPath = existingPngs[i];
            string metaPath = pngPath + ".meta";
            File.Delete(pngPath);
            if (File.Exists(metaPath))
                File.Delete(metaPath);
        }

        for (int i = 0; i < sourcePngs.Length; i++)
        {
            string file = sourcePngs[i];
            string name = Path.GetFileName(file);
            string dest = Path.Combine(targetAbsolute, name);
            File.Copy(file, dest, true);
            copiedAssetPaths.Add($"{targetAssetFolder}/{name}".Replace("\\", "/"));
        }

        Debug.Log($"Copied {sourcePngs.Length} PNG file(s) from {sourceDirectoryAbsolute} to {targetAssetFolder}");
        return true;
    }

    private static Sprite LoadSpriteByStem(string imageFolder, string stem)
    {
        if (string.IsNullOrWhiteSpace(stem))
            return null;

        string normalizedStem = stem.Trim();

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { imageFolder });
        string bestPath = null;
        int bestScore = int.MaxValue;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            if (!fileName.StartsWith(normalizedStem, StringComparison.OrdinalIgnoreCase))
                continue;

            int score = fileName.Length;
            if (bestPath == null || score < bestScore)
            {
                bestPath = path;
                bestScore = score;
            }
        }

        if (!string.IsNullOrWhiteSpace(bestPath))
            return LoadLargestSpriteAtPath(bestPath);

        string imageFolderAbsolute = ToAbsolutePath(imageFolder);
        if (Directory.Exists(imageFolderAbsolute))
        {
            string[] pngs = Directory.GetFiles(imageFolderAbsolute, "*.png", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < pngs.Length; i++)
            {
                string fileName = Path.GetFileNameWithoutExtension(pngs[i]);
                if (!fileName.StartsWith(normalizedStem, StringComparison.OrdinalIgnoreCase))
                    continue;

                string assetPath = $"{imageFolder}/{Path.GetFileName(pngs[i])}".Replace("\\", "/");
                Sprite fallback = LoadLargestSpriteAtPath(assetPath);
                if (fallback != null)
                    return fallback;
            }
        }

        return null;
    }

    private static Sprite LoadLargestSpriteAtPath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        Sprite best = null;
        float bestArea = -1f;

        for (int i = 0; i < all.Length; i++)
        {
            Sprite s = all[i] as Sprite;
            if (s == null)
                continue;

            float area = s.rect.width * s.rect.height;
            if (area > bestArea)
            {
                best = s;
                bestArea = area;
            }
        }

        return best;
    }

    private static void EnsureSpritesInFolder(string imageFolder)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { imageFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }

    private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
            return existing;

        T created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    private static string EnsureFolder(string parent, string folderName)
    {
        string full = $"{parent}/{folderName}";
        if (!AssetDatabase.IsValidFolder(full))
            AssetDatabase.CreateFolder(parent, folderName);
        return full;
    }

    private static string ToAbsolutePath(string assetRelativePath)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, assetRelativePath));
    }

    private static string Sanitize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "Case_Generated";

        foreach (char c in Path.GetInvalidFileNameChars())
            raw = raw.Replace(c.ToString(), "_");

        return raw.Replace(' ', '_');
    }

    private static string SafeString(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
    }

    private static string ResolveGeneratedStem(string rawStem, string fallbackPrefix, int oneBasedIndex)
    {
        if (string.IsNullOrWhiteSpace(rawStem))
            return $"{fallbackPrefix}_{oneBasedIndex:00000}_";

        string trimmed = rawStem.Trim();
        if (trimmed == fallbackPrefix || trimmed == fallbackPrefix + "_")
            return $"{fallbackPrefix}_{oneBasedIndex:00000}_";

        return trimmed;
    }

    [Serializable]
    private class GeneratedCaseData
    {
        public string caseId;
        public string caseTitle;
        public string caseDescription;
        public GeneratedCaseLocation location;
        public string locationAddressOrBusiness;
        public string locationCity;
        public string locationCountry;
        public string featuredImageStem;
        public string featuredImagePromptBase;
        public string verdictTitle;
        public string explanation;
        public int guiltySlot;
        public GeneratedSuspect[] suspects;
        public GeneratedEvidence[] evidence;
    }

    [Serializable]
    private class GeneratedCaseLocation
    {
        public string addressOrBusiness;
        public string city;
        public string country;
    }

    [Serializable]
    private class GeneratedSuspect
    {
        public int slot;
        public string displayName;
        public string sex;
        public string occupation;
        public string nationality;
        public string height;
        public string weight;
        public string keyPersonalityTrait;
        public string dialogue;
        public string portraitStem;
        public string portraitPromptBase;
    }

    [Serializable]
    private class GeneratedEvidence
    {
        public int slot;
        public string title;
        public string description;
        public string discoveryLocation;
        public string imageStem;
        public string imagePromptBase;
    }
}
