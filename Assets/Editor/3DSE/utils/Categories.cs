using System.Collections.Generic;
using ActionGame.MapSound;
using System;
using IllusionMods.Koikatsu3DSEModTools;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

namespace IllusionMods.Koikatsu3DSECategoryTool {

	public class Category
	{
		public string name { get; set; }
		public string author { get; set; }
		public List<StudioItemParam> items { get; set; }

		public Category(string name, string author, List<StudioItemParam> items)
		{
			this.name = name;
			this.author = author;
			this.items = items;
		}

		public Category(string name, string author)
		{
			this.name = name;
			this.author = author;
			this.items = new List<StudioItemParam>();
		}

		public Category(string name)
		{
			this.name = name;
			this.author = "";
			this.items = new List<StudioItemParam>();
		}

		public string GetKey()
		{
			if (this.author == "")
			{
				return this.name;
			}
			else
			{
				return this.name + " [" + this.author + "]";
			}
		}

		public void AddFile(StudioItemParam item)
		{
			this.items.Add(item);
		}

		public void AddFiles(List<StudioItemParam> items)
		{
			this.items.AddRange(items);
		}
	}

	public class PrefabModifier
	{
		public bool isLoop { get; set; }
		public Utils.Tuple<float> threshold { get; set; }
		public float volume { get; set; }

		public PrefabModifier(bool isLoop = false, Utils.Tuple<float> threshold = null, float volume = -1.0f)
		{
			this.isLoop = isLoop;
			this.threshold = threshold;
			this.volume = volume;
		}
	}

	public class StudioItemParam
	{
		private string _id;
		public string id { 
			get { return _id; }
			set { 
				if (value != null) int.Parse(value);
				_id = value; 
			}
		}
		private string _itemName;
		public string itemName
		{
			get
			{
				if (this.prefabModifier != null && this.prefabModifier.isLoop)
				{
					return _itemName;
				}
				else
				{
					return "(S)" + _itemName;
				}
			}
			set
			{
				_itemName = value;
			}
		}
		public string path { get; set; }
		public PrefabModifier prefabModifier { get; set; }
		public string prefabName { 
			get { return Utils.ToSnakeCase(_itemName); }
		}
		public string prefabID {
			get { return id == null ? null : Utils.AddFileID(this.prefabName, this.id); }
		}

		public StudioItemParam(string itemName, string id = null, string path = null, PrefabModifier prefabModifier = null)
		{
			this.itemName = itemName;
			this.id = id;
			this.path = path;
			this.prefabModifier = prefabModifier;
		}
	}

	public class CategoryManager
	{
		public int maxPerCategory { get; set; }

		public CategoryManager(int maxPerCategory = int.MaxValue)
		{
			this.maxPerCategory = maxPerCategory;
		}

		public List<Category> BuildFromSource(string inputPath, int maxPerCategory = int.MaxValue)
		{
			List<Category> categories = new List<Category>();
			string sourcesPath = Path.GetFullPath(inputPath);
			string[] rootFolders = Directory.GetDirectories(sourcesPath);
			List<string> tags = TagManager.GetTags(sourcesPath, new string[] { TagManager.Tags.Indexed });

			if (rootFolders.Length == 0)
			{
				tags.Add(TagManager.Tags.KeepName);
				categories.Add(new Category("Default", "", GetCategoryFiles(sourcesPath, tags)));
			}
			else
			{
				foreach (string rootFolder in rootFolders)
				{
					string rootFolderName = Path.GetFileName(rootFolder);
					tags = TagManager.GetTags(rootFolder, tags);

					Match match = Regex.Match(rootFolderName, @"^\[(.+)]$");
					if (match.Success && Directory.GetDirectories(rootFolder).Length > 0)
					{
						categories.Add(new Category(PadSectionString(match.Groups[1].Value)));
						foreach (string subfolder in Directory.GetDirectories(rootFolder))
						{
							categories.AddRange(GetCategoriesRecursive(subfolder, tags));
						}
					}
					else
					{
						categories.AddRange(GetCategoriesRecursive(rootFolder, tags));
					}
				}
			}
			return categories;
		}

		private List<Category> GetCategoriesRecursive(string folder, ICollection<string> cumulTags = null)
		{
			List<string> tags = TagManager.GetTags(folder, cumulTags);
			string folderName = Path.GetFileName(folder);
			
			Match match = Regex.Match(folderName, @"^(?<categoryName>[^()]+)(\((?<author>[^)]+)\))?$");
			string categoryName = tags.Contains(TagManager.Tags.LegacyClassifier) ? match.Groups["categoryName"].Value.Trim() : folderName;
			string author = match.Groups["author"].Value.Trim();
			List<Category> categories = new List<Category>();

			if (!tags.Contains(TagManager.Tags.LegacyClassifier) || !string.IsNullOrEmpty(author))
			{
				if (tags.Contains(TagManager.Tags.SkipFolderName))
				{
					tags.RemoveAll(item => item == TagManager.Tags.SkipFolderName);
					categories.Add(new Category(categoryName, author, GetCategoryFiles(folder, tags)));
				}
				else
				{
					categories.Add(new Category(categoryName, author, GetCategoryFiles(folder, tags, Utils.ToItemCase(categoryName))));
				}
				return categories;
			}
			else
			{
				tags.RemoveAll(item => item == TagManager.Tags.SkipFolderName);
				foreach (string subfolder in Directory.GetDirectories(folder))
				{
					categories.AddRange(GetCategoriesRecursive(subfolder, tags));
				}
			}

			return categories;
		}

		private List<StudioItemParam> GetCategoryFiles(string folder, ICollection<string> cumulTags, string pathName = "")
		{
			List<StudioItemParam> items = new List<StudioItemParam>();
			int index = 1;

			foreach (string entry in Directory.GetFileSystemEntries(folder).OrderBy(x => x, new NaturalSortComparer()))
			{
				if (Directory.Exists(entry))
				{
					string folderName = Path.GetFileName(entry);
					List<string> tags = TagManager.GetTags(entry, cumulTags);

					if (tags.Contains(TagManager.Tags.LegacyClassifier))
					{
						if (folderName.ToUpper().Contains("FX") || folderName.ToUpper().Contains("ORIGINAL"))
						{
							continue;
						}
						else if (folderName.ToUpper() == "NORMAL")
						{
							tags.Add(TagManager.Tags.SkipFolderName);
						}
					}

					if (tags.Contains(TagManager.Tags.SkipFolderName))
					{
						tags.Remove(TagManager.Tags.SkipFolderName);
						items.AddRange(GetCategoryFiles(entry, tags, pathName));
					}
					else if (folderName.ToUpper() == folderName)
					{
						items.AddRange(GetCategoryFiles(entry, tags, pathName + folderName));
					}
					else
					{
						items.AddRange(GetCategoryFiles(entry, tags, pathName + Utils.ToItemCase(folderName)));
					}
				}
				else if (AudioProcessor.IsValidAudioFile(entry) && index <= maxPerCategory)
				{
					string filename = Path.GetFileNameWithoutExtension(entry);
					Match idMatch = Regex.Match(filename, Utils.FileIDPattern);
					if (idMatch.Success && idMatch.Groups.Count > 1)
					{
						filename = filename.Substring(0, idMatch.Groups[1].Index - 1);
					}
					items.Add(new StudioItemParam(
						BuildItemName(pathName, cumulTags, filename, index++),
						idMatch.Success ? idMatch.Groups[1].Value : null,
						Utils.FullPathToAssetPath(entry), 
						TagManager.GetPrefabModifier(cumulTags)
					));
				}
			}
			return items;
		}

		private string BuildItemName(string pathName, ICollection<string> tags, string filename, int index)
		{
			string name = string.IsNullOrEmpty(pathName) ? Utils.ToItemCase(Path.GetFileNameWithoutExtension(filename)) : pathName;
			return TagManager.ApplyNameModifierTags(name, tags, filename, index);
		}

		private string PadSectionString(string input)
		{
			int totalLength = 13;
			int paddingLength = totalLength - input.Length;
			int leftPadding = paddingLength / 2;
			int rightPadding = paddingLength - leftPadding;
			if (paddingLength == 3)
			{
				rightPadding = leftPadding = 1;
			}
			else if (paddingLength >= 3)
			{
				rightPadding--;
				leftPadding--;
			}

			if (paddingLength > 2)
			{
				return new string('=', leftPadding) + " " + input + (paddingLength > 3 ? " " : "") + new string('=', rightPadding);
			}
			else
			{
				return new string('=', leftPadding) + input + new string('=', rightPadding);
			}
		}
	}

	
}