﻿using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Vintage Story Default Server Mods")]
[assembly: AssemblyDescription("www.vintagestory.at")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Tyron Madlener (Anego Studios)")]
[assembly: AssemblyProduct("Vintage Story")]
[assembly: AssemblyCopyright(GameVersion.CopyRight)]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("3730ff53-02ab-4ccd-80ab-6199df67383b")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion(GameVersion.AssemblyVersion)]
[assembly: AssemblyFileVersion(GameVersion.OverallVersion)]
[assembly: InternalsVisibleTo("VSSurvivalModTests")]

[assembly: ModInfo("Essentials", "game",
    Version = GameVersion.ShortGameVersion,
    NetworkVersion = GameVersion.NetworkVersion,
    IconPath = "game/textures/gui/modicon.png",
    Description = "Game Essentials (Assets loader, world map, weather, AI, handbook, physics,...)",
    Authors = new[] { "Tyron" })]
