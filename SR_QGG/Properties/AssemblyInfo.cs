using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("[SR] Questing Gives Goodwill")]
[assembly: AssemblyDescription("Modifies quests so that they give a bit of goodwill with each reward.\r\n\r\nI don't know how to make settings yet, so in the future it'll be configurable, but not right now:\r\n- Goodwill is worth 100 silver.\r\n- Rewards values used for goodwill are boosted by 20%.\r\n- Rewards are additionally boosted by 2 Goodwill.\r\n- Rare rewards that costs more than allowed (which happens for some reason) gets a negative goodwill with the choice.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("SirRolin")]
[assembly: AssemblyProduct("SR_QGG")]
[assembly: AssemblyCopyright("Copyright ©  2023")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("662f09cd-fe23-4573-b71d-594c10424afa")]

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
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
