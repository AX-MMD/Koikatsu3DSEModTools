using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Studio.Sound;
using System.Text;
using System.Linq;
using ActionGame.MapSound;
using IllusionMods.KoikatsuStudioCsv;
using IllusionMods.Koikatsu3DSECategoryTool;

namespace IllusionMods.Koikatsu3DSEModTools {

	public class KK3DSEModManager
	{
		public const string BackupPath = "3DSECsvBackup";

		public string modPath;
		public string modName;
		public string csvFolderPath;
		public string prefabOutputPath;
		public string sourcesPath;
		public string basePrefabPath;
		public UnityEngine.GameObject basePrefab;

		private bool disposed = false;


		private static void AssertIsModPath(string modPath)
		{
			if (Directory.GetParent(modPath).Name != "Mods")
			{
				throw new Exception("Invalid mod path: " + modPath);
			}
		}

		public static string GetSourceFolderPath(string path)
		{
			AssertIsModPath(path);
			return Path.Combine(path, "Sources");
		}

		public static string GetPrefabFolderPath(string path)
		{
			AssertIsModPath(path);
			return Path.Combine(path, "Prefab");
		}

		public static string GetBasePrefabPath(string path)
		{
			AssertIsModPath(path);
			return Path.Combine(path, "base_3dse.prefab");
		}

		public KK3DSEModManager(string selectedPath)
		{
			this.modPath = Utils.GetModPath(selectedPath, true);
			this.modName = Utils.GetModName(this.modPath);
			this.csvFolderPath = CsvUtils.GetItemDataFolder(this.modPath);
			this.prefabOutputPath = GetPrefabFolderPath(selectedPath);
			if (!Directory.Exists(prefabOutputPath))
			{
				Directory.CreateDirectory(prefabOutputPath);
				Debug.Log("Created output directory: " + prefabOutputPath);
			}
			this.sourcesPath = GetSourceFolderPath(selectedPath);
			if (!Directory.Exists(sourcesPath))
			{
				throw new Exception("Sources folder not found at path: " + sourcesPath);
			}
			this.basePrefabPath = GetBasePrefabPath(selectedPath);
			if (!File.Exists(this.basePrefabPath))
			{
				throw new Exception("Base prefab not found at path: " + this.basePrefabPath);
			}
			this.basePrefab = (GameObject)AssetDatabase.LoadAssetAtPath(this.basePrefabPath, typeof(GameObject));
			if (this.basePrefab == null)
			{
				throw new Exception("Base prefab not found at path: " + this.basePrefabPath);
			}

			this.BackupCSV();
		}

		public Utils.GenerationResult GenerateCSV(bool create, IList<Category> categories)
		{
			return CsvUtils.GenerateCSV(this.modPath, create, categories);
		}

		public Utils.GenerationResult GeneratePrefabs(bool create, IList<Category> categories)
		{
			if (!Directory.Exists(this.prefabOutputPath))
			{
				Directory.CreateDirectory(this.prefabOutputPath);
				Debug.Log("Created output directory: " + this.prefabOutputPath);
			}
			else if (create)
			{
				// Clear the output directory
				DirectoryInfo directoryInfo = new DirectoryInfo(this.prefabOutputPath);
				foreach (FileInfo file in directoryInfo.GetFiles())
				{
					file.Delete();
				}
				foreach (DirectoryInfo dir in directoryInfo.GetDirectories())
				{
					dir.Delete(true);
				}
				Debug.Log("Cleared output directory: " + this.prefabOutputPath);
			}

			string[] oldPrefabPaths = Directory.GetFiles(this.prefabOutputPath, "*.prefab", SearchOption.AllDirectories);
			HashSet<string> legacyOldPrefabs = new HashSet<string>();
			Dictionary<string, string> oldPrefabs = new Dictionary<string, string>();
			foreach (string oldPrefabPath in oldPrefabPaths)
			{
				Match idMatch = Regex.Match(Path.GetFileNameWithoutExtension(oldPrefabPath), Utils.FileIDPattern);
				if (!idMatch.Success)
				{
					legacyOldPrefabs.Add(oldPrefabPath.Replace("\\", "/"));
				}
				else
				{
					oldPrefabs[idMatch.Groups[1].Value] = oldPrefabPath.Replace("\\", "/");
				}
			}

			int prefabCreateCount = 0;
			int prefabUpdateCount = 0;
			int categoryCount = 0;
			int totalCategories = categories.Where(x => x.items.Count > 0).Count();
			string progressName = create ? "Generating " + this.modName : "Updating " + this.modName;

			foreach (Category category in categories.Where(x => x.items.Count > 0))
			{	
				string categoryOutputPath = Path.Combine(this.prefabOutputPath, category.GetKey()).Replace("\\", "/");
				string assetBundlePath = Utils.GetAssetBundlePath(this.modPath, category.GetKey());

				if (!Directory.Exists(categoryOutputPath))
				{
					Directory.CreateDirectory(categoryOutputPath);
					Debug.Log("Created output directory: " + categoryOutputPath);
				}
				else if (create)
				{
					// Clear the output directory
					DirectoryInfo directoryInfo = new DirectoryInfo(categoryOutputPath);
					foreach (FileInfo file in directoryInfo.GetFiles())
					{
						file.Delete();
					}
					foreach (DirectoryInfo dir in directoryInfo.GetDirectories())
					{
						dir.Delete(true);
					}
					Debug.Log("Cleared output directory: " + categoryOutputPath);
				}

				int itemCount = 0;
				categoryCount++;
				foreach (StudioItemParam item in category.items)
				{
					itemCount++;
					EditorUtility.DisplayProgressBar(
						progressName, 
						string.Format("Category ({0}/{1}): {2} -> ({3}/{4})", categoryCount, totalCategories, category.GetKey(), itemCount, category.items.Count),
						(float)itemCount / category.items.Count
					);

					string newPrefabPath = Path.Combine(categoryOutputPath, item.prefabID + ".prefab").Replace("\\", "/");
					string legacyPrefabPath = Path.Combine(categoryOutputPath, item.prefabName + ".prefab").Replace("\\", "/");
					GameObject prefab;

					if (oldPrefabs.ContainsKey(item.id) && File.Exists(oldPrefabs[item.id]))
					{
						prefab = (GameObject)PrefabUtility.InstantiatePrefab(
							AssetDatabase.LoadAssetAtPath(oldPrefabs[item.id], typeof(GameObject))
						);
						if (oldPrefabs[item.id] == newPrefabPath)
						{
							oldPrefabs.Remove(item.id);
						}
						prefabUpdateCount++;
					}
					else if (legacyOldPrefabs.Contains(legacyPrefabPath))
					{
						prefab = (GameObject)PrefabUtility.InstantiatePrefab(
							AssetDatabase.LoadAssetAtPath(legacyPrefabPath, typeof(GameObject))
						);
						prefabUpdateCount++;
					}
					else
					{
						prefab = (GameObject)PrefabUtility.InstantiatePrefab(this.basePrefab);
						prefabCreateCount++;
					}

					// Load the AudioClip from the .wav file
					AudioClip audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(item.path);
					if (audioClip == null)
					{
						throw new Exception("AudioClip not found at path: " + item.path);
					}

					// Assign the AudioClip to the SEComponent
					SEComponent seComponent = prefab.GetComponent<SEComponent>();
					if (seComponent != null)
					{
						seComponent._clip = audioClip;
						if (item.prefabModifier.isLoop)
						{
							seComponent._isLoop = true;
						}
						if (item.prefabModifier.threshold != null)
						{
							seComponent._rolloffDistance = new Threshold(item.prefabModifier.threshold.Item1, item.prefabModifier.threshold.Item2);
						}
						if (item.prefabModifier.volume != -1.0f)
						{
							seComponent._volume = item.prefabModifier.volume;
						}
					}
					else
					{
						throw new Exception("SEComponent not found on the instantiated prefab.");
					}

					PrefabUtility.CreatePrefab(newPrefabPath, prefab);
					UnityEngine.Object.DestroyImmediate(prefab);

					// Set the asset bundle name in the .meta file
					string metaFilePath = newPrefabPath + ".meta";
					if (File.Exists(metaFilePath))
					{
						Utils.SetAssetBundleNameInMetaFile(metaFilePath, assetBundlePath);
					}
					else
					{
						throw new Exception("Meta file not found for prefab: " + newPrefabPath);
					}
				}
			}
			// Delete any old prefabs that were not updated
			foreach (string oldPrefabPath in oldPrefabs.Values)
			{
				File.Delete(oldPrefabPath);
				File.Delete(oldPrefabPath + ".meta");
			}
			foreach (string legacyOldPrefab in legacyOldPrefabs)
			{
				File.Delete(legacyOldPrefab);
				File.Delete(legacyOldPrefab + ".meta");
			}

			// Delete empty directories
			foreach (string categoryOutputPath in Directory.GetDirectories(this.prefabOutputPath))
			{
				if (Directory.GetFiles(categoryOutputPath).Length == 0)
				{
					Directory.Delete(categoryOutputPath);
					File.Delete(categoryOutputPath + ".meta");
				}
			}

			return new Utils.GenerationResult
			{
				createCount = prefabCreateCount,
				updateCount = prefabUpdateCount,
				deleteCount = oldPrefabs.Count + legacyOldPrefabs.Count,
			};
		}

		public void BackupCSV()
		{
			string backupPath = Path.Combine(this.csvFolderPath, BackupPath);
			if (!Directory.Exists(backupPath))
			{
				Directory.CreateDirectory(backupPath);
			}
			else
			{
				foreach (string file in Directory.GetFiles(backupPath, "*.csv"))
				{
					File.Delete(file);
					File.Delete(file + ".meta");
				}
			}

			foreach (string file in Directory.GetFiles(this.csvFolderPath, "*.csv"))
			{
				File.Copy(file, Path.Combine(backupPath, Path.GetFileName(file)), true);
			}
		}

		public void RestoreBackupCSV()
		{
			string backupPath = Path.Combine(this.csvFolderPath, BackupPath);
			if (Directory.Exists(backupPath))
			{
				string[] bkFiles = Directory.GetFiles(backupPath, "*.csv");
				string[] csvFiles = Directory.GetFiles(this.csvFolderPath, "*.csv");
				for (int i = 0; i < csvFiles.Length; i++)
				{
					try 
					{
						File.Delete(csvFiles[i]);
						File.Delete(csvFiles[i] + ".meta");
						if (i < bkFiles.Length)
						{
							File.Copy(bkFiles[i], Path.Combine(this.csvFolderPath, Path.GetFileName(bkFiles[i])), true);
						}
					}
					catch (Exception e)
					{
						Debug.LogError("Failed to delete file: " + csvFiles[i] + "\n" + e.Message);
					}
				}
			}
		}

		public void DeleteBackupCSV()
		{
			string backupPath = Path.Combine(this.csvFolderPath, BackupPath);
			if (Directory.Exists(backupPath))
			{
				foreach (string file in Directory.GetFiles(backupPath, "*.csv"))
				{
					try
					{
						File.Delete(file);
						File.Delete(file + ".meta");
					}
					catch (Exception e)
					{
						Debug.LogError("Failed to delete file: " + file + "\n" + e.Message);
					}
				}
				try
				{
					Directory.Delete(backupPath, true);
					File.Delete(backupPath + ".meta");
				}
				catch (Exception e)
				{
					Debug.LogError("Failed to delete directory: " + backupPath + "\n" + e.Message);
				}
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposed)
			{
				if (disposing)
				{
					this.DeleteBackupCSV();
				}

				disposed = true;
			}
		}

		~KK3DSEModManager()
		{
			Dispose(false);
		}
	}
}