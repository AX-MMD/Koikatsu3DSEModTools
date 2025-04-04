using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IllusionMods.KoikatsuStudioCsv;
using IllusionMods.Koikatsu3DSEModTools;

public class MakeThumbs : MonoBehaviour
{
    [MenuItem("Assets/3DSE/Tools/Make Thumbnails", true)]
    private static bool ValidateMakeThumbnails()
    {
        return GetSelected().Count() > 0;
    }

    public static IEnumerable<string> GetSelected()
    {
        string sPath = Utils.GetLastSelectedPath();
        if (sPath == null || !(AssetDatabase.IsValidFolder(sPath) && Utils.IsValidModPath(sPath)))
        {
            return new string[] { };
        }
        else if (Path.GetFileName(sPath) == "Studio_Thumbs")
        {
            return new string[] { sPath };
        }
        else
        {
            return Utils.GetSelectedModPaths().Select(path => Path.Combine(path, "Studio_Thumbs"));
        }
    }

    [MenuItem("Assets/3DSE/Tools/Make Thumbnails", false, 8)]
    public static void MakeThumbnails()
    {
        IEnumerable<string> selectedPaths = GetSelected();
        foreach (string selectedPath in selectedPaths)
        {
            try
            {
                string baseThumbPath = Path.Combine(selectedPath, "base.png");
                if (!File.Exists(baseThumbPath))
                {
                    Debug.LogError("Base thumbnail not found at path: " + baseThumbPath);
                    continue;
                }

                // delete existing thumbnails
                string[] existingThumbs = Directory.GetFiles(selectedPath, "*.png", SearchOption.TopDirectoryOnly);
                foreach (string thumbPath in existingThumbs)
                {
                    if (thumbPath != baseThumbPath)
                    {
                        File.Delete(thumbPath);
                        File.Delete(thumbPath + ".meta");
                    }
                }

                CsvUtils.ItemFileAggregate itemFileAggregate = CsvUtils.GetItemFileAggregate(selectedPath);
                List<CsvUtils.StudioItem> entries = itemFileAggregate.GetEntries<CsvUtils.StudioItem>();

                float count = 1.0f;
                foreach (CsvUtils.StudioItem entry in entries)
                {
                    EditorUtility.DisplayProgressBar(
                        "Creating Thumbnails", 
                        string.Format("{0} -> ({1}/{2})", selectedPath, count, entries.Count), 
                        count / entries.Count
                    );
                    string newThumbPath = Path.Combine(
                        selectedPath, 
                        string.Format("{0}-{1}-{2}.png", entry.groupNumber.PadLeft(8, '0'), entry.categoryNumber.PadLeft(8, '0'), entry.name)
                    );
                    File.Copy(baseThumbPath, newThumbPath, true);
                    count += 1.0f;
                }

                EditorUtility.ClearProgressBar();
                Debug.Log("Thumbnails created successfully for path: " + selectedPath);
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError("Error creating thumbnails for path: " + selectedPath + "\n" + e.Message);
            }
        }

        AssetDatabase.Refresh();
    }
}