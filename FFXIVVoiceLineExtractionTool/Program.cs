using System;
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
		bool extractMahjongVoiceLines = false;
		bool extractCutsceneVoiceLines = false;
		uint expansionRangeStart = uint.MaxValue;
		uint expansionRangeEnd = uint.MaxValue;
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
						languages = ["en", "ja", "fr", "de"];
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
				case "--mahjong":
					extractMahjongVoiceLines = true;
					break;
				case "--cutscene":
					extractCutsceneVoiceLines = true;
					if ((i + 1) < args.Length && args[i + 1].Contains("ex"))
					{
						string exSelectionArg = args[++i].Replace("ex", "");
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
				ExpansionRange = new(expansionRangeStart, expansionRangeEnd)
			};
			if (extractBattleVoiceLines)
			{
				ExtractBattleVoiceLines(lumina, extractionConfiguration);
			}
			if (extractMahjongVoiceLines)
			{
				ExtractMahjongVoiceLines(lumina, extractionConfiguration);
			}
			if (extractCutsceneVoiceLines)
			{
				ExtractCutsceneVoiceLines(lumina, extractionConfiguration);
			}
		}
	}

	const uint BattleVoiceLineStartIndex = 8201000;
	// Mahjong voice lines start at 8291000, which seems to imply that the third most significant digit is a "bank" index
	static void ExtractBattleVoiceLines(Lumina.GameData gameData, ExtractionConfiguration extractionConfiguration)
	{
		const string VoLineGameDirectory = "sound/voice/vo_line/";
		string outDirectory = extractionConfiguration.OutDirectory + VoLineGameDirectory;
		System.IO.Directory.CreateDirectory(outDirectory);

		using var logFileStream = System.IO.File.Create(outDirectory
			+ DateTime.UtcNow.ToString("s", System.Globalization.CultureInfo.InvariantCulture).Replace(":", "-")
			+ "_battle.log"
		);
		logFileStream.Write(System.Text.Encoding.UTF8.GetBytes(
			VoLineGameDirectory + ", " + "sha256\n"
			));

		uint num_unused_indices = 0;
		for (uint i = BattleVoiceLineStartIndex; i < MahjongVoiceLineStartIndex; i++)
		{
			string fileName = i + "_ja.scd"; // use JP language file as "canary"
			if (gameData.FileExists(VoLineGameDirectory + fileName))
			{
				num_unused_indices = 0;
				foreach (string language in extractionConfiguration.Languages)
				{
					fileName = i + $"_{language}.scd";
					Lumina.Data.FileResource file = gameData.GetFile(VoLineGameDirectory + fileName)!;
					file!.SaveFile(outDirectory + fileName); // TODO test what the difference is to SaveFileRaw

					logFileStream.Write(System.Text.Encoding.UTF8.GetBytes(
						fileName + ", " + file!.GetFileHash() + "\n"
					));
				}
			}
			else
			{
				num_unused_indices += 1;
				if (num_unused_indices >= 100) break; // heuristic
			}
		}
	}

	const uint MahjongVoiceLineStartIndex = 8291000;
	static void ExtractMahjongVoiceLines(Lumina.GameData gameData, ExtractionConfiguration extractionConfiguration)
	{
		const string VoLineGameDirectory = "sound/voice/vo_line/";
		string outDirectory = extractionConfiguration.OutDirectory + VoLineGameDirectory;
		System.IO.Directory.CreateDirectory(outDirectory);

		using var logFileStream = System.IO.File.Create(outDirectory
			+ DateTime.UtcNow.ToString("s", System.Globalization.CultureInfo.InvariantCulture).Replace(":", "-")
			+ "_mahjong.log"
		);
		logFileStream.Write(System.Text.Encoding.UTF8.GetBytes(
			VoLineGameDirectory + ", " + "sha256\n"
			));

		uint num_unused_indices = 0;
		for (uint i = MahjongVoiceLineStartIndex; i < MahjongVoiceLineStartIndex + 10000u; i++)
		{
			string fileName = i + "_ja.scd"; // use JP language file as "canary"
			if (gameData.FileExists(VoLineGameDirectory + fileName))
			{
				num_unused_indices = 0;
				foreach (string language in extractionConfiguration.Languages)
				{
					fileName = i + $"_{language}.scd";
					Lumina.Data.FileResource file = gameData.GetFile(VoLineGameDirectory + fileName)!;
					file!.SaveFile(outDirectory + fileName); // TODO test what the difference is to SaveFileRaw

					logFileStream.Write(System.Text.Encoding.UTF8.GetBytes(
						fileName + ", " + file!.GetFileHash() + "\n"
					));
				}
			}
			else
			{
				num_unused_indices += 1;
				if (num_unused_indices >= 100) break; // heuristic
			}
		}
	}

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
				string cutsceneVoLineGameDirectory = (ex == 0 && patch_suffix < 10) ?
					$"cut/ffxiv/sound/manfst/manfst{patch_suffix:D1}00/" // 2.0 jank
					: $"cut/ex{ex}/sound/voicem/voiceman_{ex + 2:D2}{patch_suffix:D3}/".Replace("ex0", "ffxiv");
				// Note that 2.1 doesn't seem to have any voiced cutscenes so only `patch_suffix` >= 200 is actually used

				uint num_empty_banks = 0;
				for (uint bank = 0; bank < (26 + 10); bank++)
				{
					// seems to be only used up to HW (not 100% sure though), with later Expansions simply having everything in bank 0
					char bankChar = (char)(bank + 0x30) > '9' ? (char)(bank + 0x57) : (char)(bank + 0x30); // '0'-'9''a'-'z'
					uint num_in_bank = 0;

					uint num_unused_indices = 0;
					for (uint i = 0; i < 100000u; i++)
					{
						string fileName = (ex == 0 && patch_suffix < 10) ?
							$"vo_manfst{patch_suffix:D1}00_{bankChar}{i:D5}_m_ja.scd" // 2.0 jank
							: $"vo_voiceman_{ex + 2:D2}{patch_suffix:D3}_{bankChar}{i:D5}_m_ja.scd"; // use JP language file as "canary"
						if (gameData.FileExists(cutsceneVoLineGameDirectory + fileName))
						{
							num_in_ex += 1;
							num_in_patch_suffix += 1;
							num_in_bank += 1;
							num_unused_indices = 0;
							if (patch_suffix != last_patch_suffix)
							{
								System.IO.Directory.CreateDirectory(outDirectory + cutsceneVoLineGameDirectory);
								logFileStream.Write(System.Text.Encoding.UTF8.GetBytes(
									cutsceneVoLineGameDirectory + ", " + "sha256\n"
									));
								last_patch_suffix = patch_suffix;
							}
							foreach (string language in extractionConfiguration.Languages)
							{
								fileName = (ex == 0 && patch_suffix < 10) ?
									$"vo_manfst{patch_suffix:D1}00_{bankChar}{i:D5}_m_{language}.scd" // 2.0 jank
									: $"vo_voiceman_{ex + 2:D2}{patch_suffix:D3}_{bankChar}{i:D5}_m_{language}.scd";
								Lumina.Data.FileResource file = gameData.GetFile(cutsceneVoLineGameDirectory + fileName)!;
								file!.SaveFile(outDirectory + fileName); // TODO test what the difference is to SaveFileRaw

								logFileStream.Write(System.Text.Encoding.UTF8.GetBytes(
									fileName + ", " + file!.GetFileHash() + "\n"
								));
							}
						}
						else
						{
							num_unused_indices += 1;
							if (num_unused_indices >= 10) break; // heuristic
						}
					}

					if (num_in_bank == 0) num_empty_banks += 1;
					else num_empty_banks = 0;
					if (num_empty_banks >= (ex == 0 ? 8u : 1u)) break; // heuristic
																	   // cut/ffxiv/sound/voicem/voiceman_02401 starts with bank 3 for some reason and others for 2.X contain jumps as well
				}

				if (num_in_patch_suffix == 0) num_empty_patch_suffixes += 1;
				else num_empty_patch_suffixes = 0;
				if (num_empty_patch_suffixes >= (ex == 0 ? 200u : 100u)) break; // heuristic
			}

			if (num_in_ex == 0) break;
		}

	}
}
