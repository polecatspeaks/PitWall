# SimHub SDK Setup

This folder should contain the SimHub SDK DLLs required to build the plugin.

## Required Files

Copy these DLLs from your SimHub installation directory (typically `C:\Program Files (x86)\SimHub\`):

- `SimHub.Plugins.dll`
- `GameReaderCommon.dll`

## Setup Instructions

1. Install SimHub from https://www.simhubdash.com/
2. Locate your SimHub installation directory
3. Copy the required DLLs to this folder
4. Build the project with `dotnet build`

## Why These Files Aren't Included

These DLLs are proprietary to SimHub and cannot be redistributed in the repository. Each developer must obtain them from their own SimHub installation.

## Alternative: Update .csproj Reference Path

If you prefer not to copy the DLLs, you can update the `<HintPath>` in `PitWall.csproj` to point directly to your SimHub installation:

```xml
<Reference Include="SimHub.Plugins">
  <HintPath>C:\Program Files (x86)\SimHub\SimHub.Plugins.dll</HintPath>
  <Private>false</Private>
</Reference>
```
