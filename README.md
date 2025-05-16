# ffxiv-voice-line-extraction-tool
Tool for extracting voice line game files from FFXIV.

The tool is haphazardly thrown together and barely works, but maybe still useful to others.

## Build & Run
The [.NET SDK](https://dotnet.microsoft.com/) is required to build the tool.

To build and run the application:
```
dotnet run --project FFXIVVoiceLineExtractionTool --configuration Release -- --help
```
The help text should hopefully explain how to use the tool well enough. (Note that improper arguments will most likely crash the tool)

## Disclaimers
This tool (almost certainly) breaks the FFXIV ToS / User Agreement and Materials Usage License.

The output is not guaranteed to be complete. Due to the nature of how this tool works (brute-forcing all potential voice line file names, with some attempts at limiting unnecessary work) any files that don't follow the implemented formats/constraints will be missing. If you come across files that should be included in the output, consider creating an issue