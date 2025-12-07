// Example: simple SC2 build-order extractor in .NET 8 using s2protocol.NET
// ------------------------------------------
// 1. Create a new project:
//      dotnet new console -n Sc2BuildOrder
//      cd Sc2BuildOrder
//      dotnet add package s2protocol.NET
// 2. Replace Program.cs with this file.
// 3. Run:
//      dotnet run "C:\path\to\your\replay.SC2Replay"
// ------------------------------------------

// Simple in-memory representation of a build order line
public record BuildOrderEntry(
    int PlayerId,
    double TimeSeconds,
    string Kind,
    string Name
);
