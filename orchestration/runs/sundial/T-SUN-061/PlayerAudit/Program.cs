using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

string folder = Path.GetFullPath(args[0]);
int inspected = 0;
bool relay = false, gate = false, config = false;
foreach (string path in Directory.GetFiles(folder, "*.dll").Order())
{
    string name = Path.GetFileName(path);
    if (!name.StartsWith("GardenVR.") && !name.StartsWith("Assembly-CSharp")) continue;
    using var stream = File.OpenRead(path);
    using var pe = new PEReader(stream);
    var metadata = pe.GetMetadataReader();
    foreach (var handle in metadata.TypeDefinitions)
    {
        var type = metadata.GetTypeDefinition(handle);
        string typeName = metadata.GetString(type.Name);
        if (typeName.Contains("CliCoachClient")) throw new Exception($"Editor CLI leaked into {name}: {typeName}");
        relay |= typeName == "RelayCoachClient";
        gate |= typeName == "SundialCoach";
        config |= typeName == "RelayConfig";
    }
    foreach (var handle in metadata.TypeReferences)
    {
        var type = metadata.GetTypeReference(handle);
        string ns = metadata.GetString(type.Namespace), typeName = metadata.GetString(type.Name);
        if (ns == "System.Diagnostics" && (typeName == "Process" || typeName == "ProcessStartInfo"))
            throw new Exception($"Process-spawning reference in {name}: {ns}.{typeName}");
    }
    Console.WriteLine($"PASS {name}: no CLI type or Process/ProcessStartInfo reference; SHA256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()}");
    inspected++;
}
if (inspected == 0 || !relay || !gate || !config) throw new Exception("Expected coach runtime types missing");
Console.WriteLine($"PASS audited {inspected} application/package assemblies; RelayCoachClient, SundialCoach, RelayConfig present.");
