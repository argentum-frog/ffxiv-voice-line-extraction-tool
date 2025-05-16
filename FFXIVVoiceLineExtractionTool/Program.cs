using System;
using System.Collections.Generic;

namespace FFXIVVoiceLineExtractionTool;

internal class Program
{
	internal struct ExtractionConfiguration
	{
		public string OutDirectory;
		public List<string> Languages;

		public Range ExpansionRange;
	}

	// On Windows typically something like:
	// @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn" for standalone Launcher
	// or @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY XIV Online" for Steam
	const string DefaultGameDirectory = null;
	const string DefaultOutDirectory = ".";
	static readonly List<string> DefaultLanguages = ["en"];

	// Assumes arguments are: <GAME_DIRECTORY> <OUT_DIRECTORY> <LANGUAGE> [--all --battle --cutscene [exX[-Y]]]
	// with optional
	static void Main(string[] args)
	{
		string? gameDirectory = DefaultGameDirectory;
		string? outDirectory = DefaultOutDirectory;
		List<string> languages = [];
		bool extractBattleVoiceLines = false;
		bool extractCutsceneVoiceLines = false;
		int expansionRangeStart = 0;
		int expansionRangeEnd = 0;
		for (UInt32 i = 0; i < args.Length; i++)
		{
			string arg = args[i];
			switch (arg)
			{
				case "--game-directory":
					gameDirectory = args[++i];
					break;
				case "--out-directory":
					outDirectory = args[++i];
					break;
				case "--language":
					string lang_arg = args[++i];
					if (lang_arg.Equals("all"))
					{
						languages = ["en", "jp", "fr", "de"];
					}
					else
					{
						languages.Add(lang_arg);
					}
					break;
				case "--all":
					extractBattleVoiceLines = true;
					extractCutsceneVoiceLines = true;
					break;
				case "--battle":
					extractBattleVoiceLines = true;
					break;
				case "--cutscene":
					extractCutsceneVoiceLines = true;
					if ((i + 1) < args.Length && args[i + 1].Contains("ex"))
					{
						string exSelectionArg = args[++i].Replace("ex", "");
						if (exSelectionArg.Contains('-'))
						{
							string[] exSelectionRange = exSelectionArg.Split('-');
							expansionRangeStart = int.Parse(exSelectionRange[0]);
							expansionRangeEnd = int.Parse(exSelectionRange[1]) + 1;
						}
						else
						{
							expansionRangeStart = int.Parse(exSelectionArg);
							expansionRangeEnd = expansionRangeStart + 1;
						}
					}
					else
					{
						expansionRangeStart = 0;
					}
					break;
				default:
					break;
			}
		}
		if (languages.Count == 0)
		{
			languages.AddRange(DefaultLanguages);
		}
		languages.Sort();


		Lumina.GameData? lumina = new(gameDirectory.Replace('\\', '/') + "/game/sqpack");

		if (lumina == null)
		{
			Console.Error.WriteLine("Game could not be opened. Please make sure path is correct");
		}
		else
		{
			ExtractionConfiguration extractionConfiguration = new()
			{
				OutDirectory = outDirectory,
				Languages = languages,
				ExpansionRange = new(new(expansionRangeStart), new(expansionRangeEnd))
			};
			if (extractBattleVoiceLines)
			{
				ExtractBattleVoiceLines(lumina, extractionConfiguration);
			}
			if (extractCutsceneVoiceLines)
			{
				ExtractCutsceneVoiceLines(lumina, extractionConfiguration);
			}
		}
	}

	static void ExtractBattleVoiceLines(Lumina.GameData gameData, ExtractionConfiguration extractionConfiguration)
	{
		const string VoLineGameDirectory = "sound/voice/vo_line/";
		string outDirectory = extractionConfiguration.OutDirectory + VoLineGameDirectory;
		System.IO.Directory.CreateDirectory(outDirectory);

		const ulong start = 8201000;
		using var logFileStream = System.IO.File.Create(outDirectory + DateTime.UtcNow.ToString("s", System.Globalization.CultureInfo.InvariantCulture).Replace(":", "-") + ".log");
		logFileStream.Write(System.Text.Encoding.UTF8.GetBytes(
			VoLineGameDirectory + ", " + "sha256\n"
			));
		for (ulong i = start; i < start + (1u << 16); i++)
		{
			string fileName = i + "_jp.scd"; // use jp language file as "canary"
			if (gameData.FileExists(VoLineGameDirectory + fileName))
			{
				foreach (string language in extractionConfiguration.Languages)
				{
					fileName = i + $"_{language}.scd";
					Lumina.Data.FileResource file = gameData.GetFile(VoLineGameDirectory + fileName)!;
					file!.SaveFile(outDirectory + fileName); // TODO test what the difference is to SaveFileRaw

					logFileStream.Write(System.Text.Encoding.UTF8.GetBytes(
						fileName + ", " + file!.GetFileHash()
					));
				}
			}
		}
	}

	static void ExtractCutsceneVoiceLines(Lumina.GameData gameData, ExtractionConfiguration extractionConfiguration)
	{
		throw new NotImplementedException("Extracting cutscene voice lines is not yet implemented!");
	}
}
