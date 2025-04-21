using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using IllusionMods.Koikatsu3DSECategoryTool;


namespace IllusionMods.Koikatsu3DSEModTools {

	public static class TagManager {

		public static class Tags {
			// tag%%value kind of tags
			public const string Append = "append";
			public const string Prepend = "prepend";
			// regular tags
			public const string AppendFilename = "appendfilename";
			public const string PrependFilename = "prependfilename";
			public const string Indexed = "indexed";
			public const string Loop = "loop";
			public const string KeepName = "keep-name";
			public const string FormatKeepName = "format-keep-name";
			// folder action tags
			public const string SkipFolderName = "skip-folder-name";
			public const string LegacyClassifier = "legacy-classifier";
			// negation tags
			public const string NoIndexed = "no-indexed";
			public const string NoLoop = "no-loop";
			public const string NoKeepName = "no-keep-name";
			public const string Reset = "reset";
			// extra action tags
			public const string Format = "format";

			public static HashSet<string> ToHashSet() {
				// add every fields in this class except for the ToHashSet method
				return new HashSet<string>(typeof(Tags)
					.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
					.Where(f => f.FieldType == typeof(string))
					.Select(f => (string)f.GetValue(null)));
			}
		}

		public static HashSet<string> ValueTags = new HashSet<string> { Tags.Append, Tags.Prepend };
		public static HashSet<string> ValidTags = Tags.ToHashSet();
		public const string FileExtention = ".3dsetags";

		public class ValidationError : Exception
		{
			public ValidationError(string message) : base(message) { }
		}

		public static List<string> GetTags(string folderPath, ICollection<string> cumulTags = null)
		{
			return cumulTags == null ? LoadTags(folderPath) : CombineTags(cumulTags, LoadTags(folderPath));
		}

		public static List<string> LoadTags(string folderPath)
		{
			List<string> tags = new List<string>();
			string tag;
			foreach (string file in Directory.GetFiles(folderPath, "*" + FileExtention))
			{
				MatchCollection matches = Regex.Matches(Path.GetFileName(file), @"\[(?<tags>[^\]]+)\]");
				foreach (Match match in matches)
				{
					tag = match.Groups["tags"].Value;
					if (!IsValidTag(tag))
					{
						throw new ValidationError(string.Format("Invalid tag '{0}' in {1}\n\nValid tags are:\n {2}", tag, folderPath, string.Join("\n  ", new List<string>(ValidTags).ToArray())));
					}
					tags.Add(match.Groups["tags"].Value);
				}
			}
			return tags;
		}

		public static List<string> CombineTags(ICollection<string> tags1, ICollection<string> tags2)
		{
			List<string> combinedTags = new List<string>(tags1);			
			if (tags1.Contains(Tags.LegacyClassifier) && tags2.Contains(Tags.Loop))
			{
				combinedTags.RemoveAll(item => item == Tags.Indexed);
			}

			foreach (string tag in tags2)
			{
				if (tag == Tags.Reset)
				{
					combinedTags = new List<string>();
				}
				else if (tag == Tags.NoIndexed)
				{
					combinedTags.RemoveAll(item => item == Tags.Indexed);
				}
				else if (tag == Tags.Indexed)
				{
					combinedTags.RemoveAll(item => item == Tags.NoIndexed || item == Tags.KeepName || item == Tags.FormatKeepName);
				}
				else if (tag == Tags.NoLoop)
				{
					combinedTags.RemoveAll(item => item == Tags.Loop);
				}
				else if (tag == Tags.Loop)
				{
					combinedTags.RemoveAll(item => item == Tags.NoLoop);
				}
				else if (tag == Tags.NoKeepName)
				{
					combinedTags.RemoveAll(item => item == Tags.KeepName || item == Tags.FormatKeepName);
				}
				else if (tag == Tags.KeepName)
				{
					combinedTags.RemoveAll(item => item == Tags.FormatKeepName || item == Tags.Indexed || item == Tags.NoKeepName);
				}
				else if (tag == Tags.FormatKeepName)
				{
					combinedTags.RemoveAll(item => item == Tags.KeepName || item == Tags.Indexed || item == Tags.NoKeepName);
				}

				if (combinedTags.Count == 0 || combinedTags.Last() != tag)
				{
					combinedTags.Add(tag);
				}
			}

			return combinedTags;
		}

		public static void EditTags(string folderPath, IEnumerable<string> tags)
		{
			EditTags(folderPath, "[" + string.Join("][", tags.ToArray()) + "]");
		}

		public static void EditTags(string folderPath, string tagsInput)
		{
			if (string.IsNullOrEmpty(tagsInput) || tagsInput == "[]")
			{
				foreach (string file in Directory.GetFiles(folderPath, "*" + FileExtention))
				{
					File.Delete(file);
					if (File.Exists(file + ".meta"))
					{
						File.Delete(file + ".meta");
					}
				}
			}
			else 
			{
				if (!Regex.IsMatch(tagsInput, @"^\[.*\]$"))
				{
					throw new ValidationError("Tags must be enclosed in brackets.");
				}
				else if (!IsValidTagsString(tagsInput))
				{	
					throw new ValidationError(string.Format("Invalid tags '{0}'\n\nValid tags are:\n {1}", tagsInput, string.Join("\n  ", new List<string>(ValidTags).ToArray())));
				}

				string tagFilePath = Path.Combine(folderPath, tagsInput + FileExtention);
				if (!File.Exists(tagFilePath))
				{
					File.Create(tagFilePath).Close();
				}
				foreach (string file in Directory.GetFiles(folderPath, "*" + FileExtention))
				{
					if (file != tagFilePath)
					{
						File.Delete(file);
						if (File.Exists(file + ".meta"))
						{
							File.Delete(file + ".meta");
						}
					}
				}
			}

			AssetDatabase.Refresh();
		}

		public static bool IsValidTag(string tag)
		{
			return ValidTags.Contains(tag) || ValidTags.Contains(tag.Split(new string[] { "%%" }, StringSplitOptions.None)[0]);
		}

		public static bool IsValidTags(IEnumerable<string> tags)
		{
			foreach (string tag in tags)
			{
				if (!IsValidTag(tag))
				{
					return false;
				}
			}
			return true;
		}

		public static bool IsValidTagsString(string tagsString)
		{
			if (!Regex.IsMatch(tagsString, @"^\[.*\]$"))
			{
				return false;
			}

			return IsValidTags(tagsString.Substring(1, tagsString.Length - 2).Split(new string[] { "][" }, StringSplitOptions.None));
		}

		public static string ApplyNameModifierTags(string itemName, ICollection<string> tags, string filename, int index)
		{
			if (tags.Contains(Tags.KeepName))
			{
				if (tags.Contains(Tags.Format))
				{
					return Utils.ToItemCase(filename) + (tags.Contains(Tags.Indexed) ? index.ToString("D2") : "");
				}
				else
				{
					return filename + (tags.Contains(Tags.Indexed) ? index.ToString("D2") : "");
				}		
			}
			// legacy code
			else if (tags.Contains(Tags.FormatKeepName))
			{
				return Utils.ToItemCase(filename) + (tags.Contains(Tags.Indexed) ? index.ToString("D2") : "");
			}

			// Iterate in reverse, deeper tags should be applied first
			string name = itemName;
			string[] tagsArray = tags.ToArray();
			for (int i = tagsArray.Count() - 1; i >= 0; i--)
			{
				string tag = tagsArray[i];
				Match appendMatch = Regex.Match(tag, @"" + Tags.Append + "%%(?<appendValue>.+)");
				if (appendMatch.Success)
				{
					name += appendMatch.Groups["appendValue"].Value;
				}

				Match prependMatch = Regex.Match(tag, @"" + Tags.Prepend + "%%(?<prependValue>.+)");
				if (prependMatch.Success)
				{
					name = prependMatch.Groups["prependValue"].Value + name;
				}
			}

			if (tags.Contains(Tags.AppendFilename))
			{
				name += filename;
			}
			if (tags.Contains(Tags.PrependFilename))
			{
				name = filename + name;
			}

			name = tags.Contains(Tags.Indexed) ? name + index.ToString("D2") : name;
			return tags.Contains(Tags.Format) ? Utils.ToItemCase (name) : name;
		}

		public static PrefabModifier GetPrefabModifier(ICollection<string> tags)
		{
			bool isLoop = tags.Contains(Tags.Loop);
			float volume = -1.0f;
			Utils.Tuple<float> threshold = null;

			// Deprecated volume and threshold tags in favor of editing prefabs directly and use UpdateFromSource

			// foreach (string tag in tags)
			// {
			//     Match volumeMatch = Regex.Match(tag, @"volume%%(?<volumeValue>.+)");
			//     if (volumeMatch.Success)
			//     {
			//         volume = float.Parse(volumeMatch.Groups["volumeValue"].Value);
			//     }

			//     Match thresholdMatch = Regex.Match(tag, @"threshold%%(?<minValue>\d+(\.\d+)?)-(?<maxValue>\d+(\.\d+)?)");
			//         if (thresholdMatch.Success)
			//         {
			//             threshold = new Utils.Tuple<float>(float.Parse(thresholdMatch.Groups["minValue"].Value), float.Parse(thresholdMatch.Groups["maxValue"].Value));
			//             break;
			//         }
			// }
			return new PrefabModifier(isLoop, threshold, volume);
		}
	}
}