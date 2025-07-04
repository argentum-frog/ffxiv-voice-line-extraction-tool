﻿using System;
using System.Collections.Generic;

namespace FFXIVVoiceLineExtractionTool;

internal class Program
{
	internal struct ExtractionConfiguration
	{
		public string OutDirectory;
		public List<string> Languages;

		public Tuple<uint, uint> ExpansionRange;
	}

	// On Windows typically something like:
	// @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn" for standalone Launcher
	// or @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY XIV Online" for Steam
	const string DefaultGameDirectory = null;
	const string DefaultOutDirectory = null;
	static readonly List<string> DefaultLanguages = ["en"];

	const string HelpText =
	"""
	Tool for extracting voice line game files from FFXIV. Not intended for general use.

	Usage: FFXIVVoiceLineExtractionTool --game-directory <GAME_DIRECTORY> --out-directory <OUT_DIRECTORY> [OPTIONS] [TARGET]

	Paths can be absolute or relative. <GAME_DIRECTORY> should be the root game directory, i.e. the one containing the `game` folder
	With [TARGET]:
		--all
			Extract all supported voice lines
		--battle
			Extract battle voice lines
		--mahjong
			Extract mahjong voice lines
		--cutscene [exX[-exY]]
			Extract cutscene voice lines.
			By default voice lines are extracted for all expansions, the optional `[exX[-exY]]` argument allows for limiting this to a single one or range where X/Y are the number of the expansions. 2.X is represented by `ex0` or `ffxiv`
	With optional [OPTIONS]:
		--language <en|ja|de|fr|all>
			Select the languages for which the voice lines should be extracted. If not provided `en` is used as the default. `all` selects the extraction of all voice lines for the supported languages. Providing the argument multiple times allows selecting a custom subset of languages
	""";

	static void Main(string[] args)
	{
		string? gameDirectory = DefaultGameDirectory;
		string? outDirectory = DefaultOutDirectory;
		List<string> languages = [];
		bool extractBattleVoiceLines = false;
		bool extractMahjongVoiceLines = false;
		bool extractCutsceneVoiceLines = false;
		uint expansionRangeStart = uint.MaxValue;
		uint expansionRangeEnd = uint.MaxValue;
		for (uint i = 0; i < args.Length; i++)
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
						languages = ["en", "ja", "fr", "de"];
					}
					else
					{
						languages.Add(lang_arg);
					}
					break;
				case "--all":
					extractBattleVoiceLines = true;
					extractMahjongVoiceLines = true;
					extractCutsceneVoiceLines = true;
					expansionRangeStart = 0;
					break;
				case "--battle":
					extractBattleVoiceLines = true;
					break;
				case "--mahjong":
					extractMahjongVoiceLines = true;
					break;
				case "--cutscene":
					extractCutsceneVoiceLines = true;
					if ((i + 1) < args.Length && args[i + 1].Contains("ex"))
					{
						string exSelectionArg = args[++i].Replace("ffxiv", "ex0").Replace("ex", "");
						if (exSelectionArg.Contains('-'))
						{
							string[] exSelectionRange = exSelectionArg.Split('-');
							expansionRangeStart = uint.Parse(exSelectionRange[0]);
							expansionRangeEnd = uint.Parse(exSelectionRange[1]) + 1;
						}
						else
						{
							expansionRangeStart = uint.Parse(exSelectionArg);
							expansionRangeEnd = expansionRangeStart + 1;
						}
					}
					else
					{
						expansionRangeStart = 0;
					}
					break;
				default:
					Console.WriteLine(HelpText);
					return;
			}
		}
		if (args.Length == 0
			|| gameDirectory == null
			|| outDirectory == null
			|| !(extractBattleVoiceLines || extractMahjongVoiceLines || extractCutsceneVoiceLines)
		)
		{
			Console.WriteLine(HelpText);
			return;
		}
		if (languages.Count == 0)
		{
			languages.AddRange(DefaultLanguages);
		}
		languages.Sort();

		gameDirectory = gameDirectory.Replace('\\', '/');
		if (!gameDirectory.EndsWith('/'))
		{
			gameDirectory += "/";
		}
		outDirectory = outDirectory.Replace('\\', '/');
		if (!outDirectory.EndsWith('/'))
		{
			outDirectory += "/";
		}

		Lumina.GameData? lumina = new(gameDirectory + "game/sqpack");

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
				ExpansionRange = new(expansionRangeStart, expansionRangeEnd)
			};
			if (extractBattleVoiceLines)
			{
				Console.WriteLine("Extracting battle voice lines...");
				ExtractBattleVoiceLines(lumina, extractionConfiguration);
				Console.WriteLine("\nFinished extracting battle voice lines");
			}
			if (extractMahjongVoiceLines)
			{
				Console.WriteLine("Extracting mahjong voice lines...");
				ExtractMahjongVoiceLines(lumina, extractionConfiguration);
				Console.WriteLine("\nFinished extracting mahjong voice lines");
			}
			if (extractCutsceneVoiceLines)
			{
				Console.WriteLine("Extracting cutscene voice lines...");
				ExtractCutsceneVoiceLines(lumina, extractionConfiguration);
				Console.WriteLine("\nFinished extracting cutscene voice lines");
			}
		}
	}

	const uint BattleVoiceLineStartIndex = 8201000u;
	// Mahjong voice lines start at 8291000, which seems to imply that the third most significant digit is a "bank" index
	static void ExtractBattleVoiceLines(Lumina.GameData gameData, ExtractionConfiguration extractionConfiguration)
	{
		const string VoLineGameDirectory = "sound/voice/vo_line/";
		string outDirectory = extractionConfiguration.OutDirectory + VoLineGameDirectory;
		System.IO.Directory.CreateDirectory(outDirectory);

		using var logFileStream = System.IO.File.Create(extractionConfiguration.OutDirectory
			+ DateTime.UtcNow.ToString("s", System.Globalization.CultureInfo.InvariantCulture).Replace(":", "-")
			+ "_battle.log"
		);
		logFileStream.Write(System.Text.Encoding.UTF8.GetBytes(
			VoLineGameDirectory + ", " + "sha256\n"
			));

		uint num_unused_indices = 0;
		for (uint i = BattleVoiceLineStartIndex; i < MahjongVoiceLineStartIndex; i++)
		{
			bool fileExistsForAtLeastOneSelectedLanguage = false;
			foreach (string language in extractionConfiguration.Languages)
			{
				string fileName = i + $"_{language}.scd";
				Lumina.Data.FileResource? file = null;
				try
				{
					file = gameData.GetFile(VoLineGameDirectory + fileName);
				}
				catch (System.IO.FileNotFoundException fileNotFoundException)
				{
					logFileStream.Write(System.Text.Encoding.UTF8.GetBytes(
						fileName + ", ERROR: " + fileNotFoundException.Message + "\n"
					));
				}
				if (file != null)
				{
					Console.Write("\r" + VoLineGameDirectory + fileName);
					fileExistsForAtLeastOneSelectedLanguage = true;
					file.SaveFile(outDirectory + fileName);

					logFileStream.Write(System.Text.Encoding.UTF8.GetBytes(
						fileName + ", " + file.GetFileHash() + "\n"
					));
				}
			}
			if (fileExistsForAtLeastOneSelectedLanguage)
			{
				num_unused_indices = 0;
			}
			else
			{
				num_unused_indices += 1;
				if (num_unused_indices >= 1000u) break;
				// heuristic
			}
		}
	}

	// TODO refactor so this is not just mostly copy-paste
	const uint MahjongVoiceLineStartIndex = 8291000u;
	static void ExtractMahjongVoiceLines(Lumina.GameData gameData, ExtractionConfiguration extractionConfiguration)
	{
		const string VoLineGameDirectory = "sound/voice/vo_line/";
		string outDirectory = extractionConfiguration.OutDirectory + VoLineGameDirectory;
		System.IO.Directory.CreateDirectory(outDirectory);

		using var logFileStream = System.IO.File.Create(extractionConfiguration.OutDirectory
			+ DateTime.UtcNow.ToString("s", System.Globalization.CultureInfo.InvariantCulture).Replace(":", "-")
			+ "_mahjong.log"
		);
		logFileStream.Write(System.Text.Encoding.UTF8.GetBytes(
			VoLineGameDirectory + ", " + "sha256\n"
			));

		uint num_unused_indices = 0;
		for (uint i = MahjongVoiceLineStartIndex; i < MahjongVoiceLineStartIndex + 10000u; i++)
		{
			bool fileExistsForAtLeastOneSelectedLanguage = false;
			foreach (string language in extractionConfiguration.Languages)
			{
				string fileName = i + $"_{language}.scd";
				Lumina.Data.FileResource? file = null;
				try
				{
					file = gameData.GetFile(VoLineGameDirectory + fileName);
				}
				catch (System.IO.FileNotFoundException fileNotFoundException)
				{
					logFileStream.Write(System.Text.Encoding.UTF8.GetBytes(
						fileName + ", ERROR: " + fileNotFoundException.Message + "\n"
					));
				}
				if (file != null)
				{
					Console.Write("\r" + VoLineGameDirectory + fileName);
					fileExistsForAtLeastOneSelectedLanguage = true;
					file.SaveFile(outDirectory + fileName);

					logFileStream.Write(System.Text.Encoding.UTF8.GetBytes(
						fileName + ", " + file.GetFileHash() + "\n"
					));
				}
			}
			if (fileExistsForAtLeastOneSelectedLanguage)
			{
				num_unused_indices = 0;
			}
			else
			{
				num_unused_indices += 1;
				if (num_unused_indices >= 1000u) break;
				// heuristic
			}
		}
	}

	static readonly uint[] ARRBasePatchSuffixNumbers = [
		0, 5, 7, 9, 50,
		200, 206, 207,
		300, 304, 306, 309, 313,
		401, 404, 405, 407, 408,
		503,
		600,
	];
	// 2.0 voice lines are stored completely separately and use a different folder numbering scheme
	static void ExtractCutsceneVoiceLines(Lumina.GameData gameData, ExtractionConfiguration extractionConfiguration)
	{
		string outDirectory = extractionConfiguration.OutDirectory;
		var expansionRange = extractionConfiguration.ExpansionRange;

		System.IO.Directory.CreateDirectory(outDirectory);
		using var logFileStream = System.IO.File.Create(
			outDirectory
			+ DateTime.UtcNow.ToString("s", System.Globalization.CultureInfo.InvariantCulture).Replace(":", "-")
			+ "_cutscene.log"
		);
		for (uint ex = expansionRange.Item1; ex < expansionRange.Item2; ex++)
		{
			uint num_in_ex = 0;
			uint num_empty_patch_suffixes = 0;
			uint last_patch_suffix = uint.MaxValue;
			for (uint patch_suffix = 0; patch_suffix < 1000u; patch_suffix++)
			{
				uint num_in_patch_suffix = 0;
				string cutsceneVoLineGameDirectory = (ex == 0 && patch_suffix < ARRBasePatchSuffixNumbers.Length) ?
					$"cut/ffxiv/sound/manfst/manfst{ARRBasePatchSuffixNumbers[patch_suffix]:D3}/" // 2.0 jank
					: $"cut/ex{ex}/sound/voicem/voiceman_{ex + 2:D2}{patch_suffix:D3}/".Replace("ex0", "ffxiv");
				// Note that 2.1 doesn't seem to have any voiced cutscenes so only `patch_suffix` >= 200 is actually used for 2.2+

				uint num_empty_banks = 0;
				for (uint bank = 0; bank < (26 + 10); bank++)
				{
					// seems to be only used up to HW (not 100% sure though), with later Expansions simply having everything in bank 0
					char bankChar = (char)(bank + 0x30) > '9' ? (char)(bank + 0x57) : (char)(bank + 0x30); // '0'-'9''a'-'z'
					uint num_in_bank = 0;

					uint num_unused_indices = 0;
					for (uint i = 0; i < 100000u; i++)
					{
						bool fileExistsForAtLeastOneSelectedLanguage = false;
						foreach (string language in extractionConfiguration.Languages)
						{
							string fileName = (ex == 0 && patch_suffix < ARRBasePatchSuffixNumbers.Length) ?
								$"vo_manfst{ARRBasePatchSuffixNumbers[patch_suffix]:D3}_{bankChar}{i:D5}_m_{language}.scd" // 2.0 jank
								: $"vo_voiceman_{ex + 2:D2}{patch_suffix:D3}_{bankChar}{i:D5}_m_{language}.scd";
							Lumina.Data.FileResource? file = null;
							try
							{
								file = gameData.GetFile(cutsceneVoLineGameDirectory + fileName);
							}
							catch (System.IO.FileNotFoundException fileNotFoundException)
							{
								logFileStream.Write(System.Text.Encoding.UTF8.GetBytes(
									fileName + ", ERROR: " + fileNotFoundException.Message + "\n"
								));
							}
							if (file != null)
							{
								if (patch_suffix != last_patch_suffix)
								{
									System.IO.Directory.CreateDirectory(outDirectory + cutsceneVoLineGameDirectory);
									logFileStream.Write(System.Text.Encoding.UTF8.GetBytes(
										cutsceneVoLineGameDirectory + ", " + "sha256\n"
										));
									last_patch_suffix = patch_suffix;
								}
								Console.Write("\r" + cutsceneVoLineGameDirectory + fileName);
								fileExistsForAtLeastOneSelectedLanguage = true;
								file!.SaveFile(outDirectory + cutsceneVoLineGameDirectory + fileName);

								logFileStream.Write(System.Text.Encoding.UTF8.GetBytes(
									fileName + ", " + file!.GetFileHash() + "\n"
								));
							}

						}
						if (fileExistsForAtLeastOneSelectedLanguage)
						{
							num_in_ex += 1;
							num_in_patch_suffix += 1;
							num_in_bank += 1;
							num_unused_indices = 0;
						}
						else
						{
							num_unused_indices += 1;
							if (num_unused_indices >= 1000u) break;
							// heuristic
						}
					}

					if (num_in_bank == 0) num_empty_banks += 1;
					else num_empty_banks = 0;
					if (num_empty_banks >= (ex == 0 ? 8u : 1u)) break;
					// heuristic
					// cut/ffxiv/sound/voicem/voiceman_02401 starts with bank 3 for some reason and others for 2.X contain jumps as well
				}

				if (num_in_patch_suffix == 0) num_empty_patch_suffixes += 1;
				else num_empty_patch_suffixes = 0;
				if (num_empty_patch_suffixes >= (ex == 0 ? 1000u : 100u)) break;
				// heuristic
			}

			if (num_in_ex == 0) break;
		}
	}
}
