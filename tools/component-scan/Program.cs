using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

// Component scan spike: what real LiveSplit component DLLs need from LiveSplit.Core, WinForms,
// System.Drawing and Windows, read from their metadata only (nothing is loaded or run).
if (args.Length == 0)
{
    Console.Error.WriteLine("usage: ComponentScan <folder or dll>...");
    return 1;
}

// --check <compat folder>: which of the components' LiveSplit.Core / WinForms / Drawing /
// UpdateManager references the look-alikes there do not have (by type and member name).
string? compat = null;
if (args.Length >= 2 && args[0] == "--check")
{
    compat = args[1];
    args = args[2..];
}

var scans = new List<Scan>();
foreach (string arg in args)
{
    IEnumerable<string> files = Directory.Exists(arg)
        ? Directory.EnumerateFiles(arg, "*.dll", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal)
        : [arg];
    foreach (string file in files)
    {
        if (Scan.Read(file) is { } scan)
        {
            scans.Add(scan);
        }
        else
        {
            Console.WriteLine($"# skipped (native or not .NET): {file}");
        }
    }
}

if (compat is not null)
{
    var context = new CheckContext(compat);
    string[] lookAlikes = ["LiveSplit.Core", "System.Windows.Forms", "System.Drawing", "UpdateManager", "WinFormsColor"];
    var assemblies = lookAlikes.ToDictionary(n => n, n => context.LoadFromAssemblyPath(Path.GetFullPath(Path.Combine(compat, n + ".dll"))));
    foreach (Scan scan in scans.Where(s => s.AssemblyRefs.Contains("LiveSplit.Core")))
    {
        var missing = new List<string>();
        foreach (string scope in lookAlikes)
        {
            foreach (string api in scan.Apis(scope))
            {
                string[] parts = api.Split("::");
                string typeName = parts[0];
                int generic = typeName.IndexOf('<');
                if (generic >= 0)
                {
                    typeName = typeName[..generic];
                }

                Type? type = assemblies[scope].GetType(typeName.Replace('+', '+'), false);
                if (type is null)
                {
                    missing.Add(api);
                    continue;
                }

                if (parts.Length > 1)
                {
                    string member = parts[1].Replace(" (field)", "");
                    if (member is "Address" or "Get" or "Set")
                    {
                        continue;
                    }

                    bool found = false;
                    for (Type? t = type; t is not null && !found; t = t.BaseType)
                    {
                        found = t.GetMember(member, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static).Length > 0;
                    }

                    found |= type.GetInterfaces().Any(i => i.GetMember(member).Length > 0);
                    if (!found)
                    {
                        missing.Add(api);
                    }
                }
            }
        }

        Console.WriteLine($"## {scan.Name}: {missing.Count} missing");
        foreach (string api in missing)
        {
            Console.WriteLine("   " + api);
        }
    }

    return 0;
}

foreach (Scan scan in scans)
{
    scan.Print();
}

Console.WriteLine();
Console.WriteLine("################ TOTALS over " + scans.Count + " assemblies");
foreach (string scope in Scan.Interesting)
{
    var uses = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
    foreach (Scan scan in scans)
    {
        foreach (string api in scan.Apis(scope))
        {
            if (!uses.TryGetValue(api, out SortedSet<string>? users))
            {
                uses[api] = users = new SortedSet<string>(StringComparer.Ordinal);
            }

            users.Add(scan.Name);
        }
    }

    int types = uses.Keys.Count(k => !k.Contains("::", StringComparison.Ordinal));
    Console.WriteLine();
    Console.WriteLine($"== {scope}: {types} types, {uses.Count - types} members, used by {uses.Values.SelectMany(u => u).Distinct().Count()} assemblies");
    foreach ((string api, SortedSet<string> users) in uses.OrderByDescending(p => p.Value.Count).ThenBy(p => p.Key, StringComparer.Ordinal))
    {
        Console.WriteLine($"  {users.Count,2}  {api}   [{string.Join(", ", users)}]");
    }
}

Console.WriteLine();
Console.WriteLine("== other referenced assemblies");
foreach (IGrouping<string, string> group in scans
    .SelectMany(s => s.AssemblyRefs.Select(r => (Ref: r, s.Name)))
    .Where(p => !Scan.Interesting.Contains(p.Ref))
    .GroupBy(p => p.Ref, p => p.Name)
    .OrderBy(g => g.Key, StringComparer.Ordinal))
{
    Console.WriteLine($"  {group.Key}   [{string.Join(", ", group.Distinct())}]");
}

Console.WriteLine();
Console.WriteLine("== P/Invoke");
foreach (Scan scan in scans)
{
    foreach (string import in scan.PInvokes)
    {
        Console.WriteLine($"  {scan.Name}: {import}");
    }
}

return 0;

internal sealed class Scan
{
    public static readonly string[] Interesting =
    [
        "LiveSplit.Core", "UpdateManager", "System.Windows.Forms", "System.Drawing", "WinFormsColor", "PresentationFramework",
        "PresentationCore", "WindowsBase", "System.Web.Extensions",
    ];

    private readonly Dictionary<string, SortedSet<string>> _apis = new(StringComparer.Ordinal);

    public string Name { get; private init; } = "";
    public string Path { get; private init; } = "";
    public string Framework { get; private set; } = "";
    public List<string> AssemblyRefs { get; } = [];
    public List<string> DefinedTypes { get; } = [];
    public SortedSet<string> PInvokes { get; } = new(StringComparer.Ordinal);

    public IEnumerable<string> Apis(string scope) => _apis.TryGetValue(scope, out SortedSet<string>? set) ? set : [];

    public static Scan? Read(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata)
        {
            return null;
        }

        MetadataReader md = pe.GetMetadataReader();
        if (!md.IsAssembly)
        {
            return null;
        }

        var scan = new Scan { Name = md.GetString(md.GetAssemblyDefinition().Name), Path = path };
        foreach (AssemblyReferenceHandle handle in md.AssemblyReferences)
        {
            AssemblyReference reference = md.GetAssemblyReference(handle);
            scan.AssemblyRefs.Add(md.GetString(reference.Name));
        }

        foreach (CustomAttributeHandle handle in md.GetAssemblyDefinition().GetCustomAttributes())
        {
            CustomAttribute attribute = md.GetCustomAttribute(handle);
            if (TypeOf(md, attribute.Constructor) == "System.Runtime.Versioning.TargetFrameworkAttribute")
            {
                BlobReader blob = md.GetBlobReader(attribute.Value);
                blob.ReadUInt16();
                scan.Framework = blob.ReadSerializedString() ?? "";
            }
        }

        var provider = new NameProvider(md);
        foreach (TypeReferenceHandle handle in md.TypeReferences)
        {
            if (Scope(md, handle) is { } scope)
            {
                scan.Add(scope, FullName(md, handle));
            }
        }

        foreach (MemberReferenceHandle handle in md.MemberReferences)
        {
            MemberReference member = md.GetMemberReference(handle);
            (string? scope, string type) = member.Parent.Kind switch
            {
                HandleKind.TypeReference => (Scope(md, (TypeReferenceHandle)member.Parent), FullName(md, (TypeReferenceHandle)member.Parent)),
                HandleKind.TypeSpecification => provider.SpecScope((TypeSpecificationHandle)member.Parent),
                _ => ((string?)null, ""),
            };
            if (scope is not null)
            {
                string kind = member.GetKind() == MemberReferenceKind.Field ? " (field)" : "";
                scan.Add(scope, $"{type}::{md.GetString(member.Name)}{kind}");
            }
        }

        foreach (TypeDefinitionHandle handle in md.TypeDefinitions)
        {
            TypeDefinition type = md.GetTypeDefinition(handle);
            string name = md.GetString(type.Name);
            if (name == "<Module>")
            {
                continue;
            }

            var bases = new List<string>();
            if (!type.BaseType.IsNil)
            {
                bases.Add(TypeName(md, type.BaseType, provider));
            }

            bases.AddRange(type.GetInterfaceImplementations()
                .Select(i => TypeName(md, md.GetInterfaceImplementation(i).Interface, provider)));
            string interesting = string.Join(", ", bases.Where(b => b.StartsWith("LiveSplit.", StringComparison.Ordinal)
                || b.StartsWith("System.Windows.Forms.", StringComparison.Ordinal)
                || b.StartsWith("System.Windows.Controls.", StringComparison.Ordinal)));
            if (interesting.Length > 0)
            {
                scan.DefinedTypes.Add($"{md.GetString(type.Namespace)}.{name} : {interesting}");
            }
        }

        foreach (MethodDefinitionHandle handle in md.MethodDefinitions)
        {
            MethodDefinition method = md.GetMethodDefinition(handle);
            MethodImport import = method.GetImport();
            if (!import.Module.IsNil)
            {
                scan.PInvokes.Add($"{md.GetString(md.GetModuleReference(import.Module).Name)}!{md.GetString(import.Name)}");
            }
        }

        return scan;
    }

    public void Print()
    {
        Console.WriteLine();
        Console.WriteLine($"################ {Name}  ({System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(Path))}/{System.IO.Path.GetFileName(Path)})");
        Console.WriteLine($"framework: {Framework}");
        Console.WriteLine($"references: {string.Join(", ", AssemblyRefs)}");
        foreach (string type in DefinedTypes)
        {
            Console.WriteLine($"  defines {type}");
        }

        foreach (string scope in Interesting)
        {
            List<string> apis = [.. Apis(scope)];
            if (apis.Count > 0)
            {
                Console.WriteLine($"  -- {scope} ({apis.Count})");
                foreach (string api in apis)
                {
                    Console.WriteLine($"     {api}");
                }
            }
        }

        foreach (string import in PInvokes)
        {
            Console.WriteLine($"  pinvoke {import}");
        }
    }

    private void Add(string scope, string api)
    {
        if (!Interesting.Contains(scope))
        {
            return;
        }

        if (!_apis.TryGetValue(scope, out SortedSet<string>? set))
        {
            _apis[scope] = set = new SortedSet<string>(StringComparer.Ordinal);
        }

        set.Add(api);
    }

    internal static string? Scope(MetadataReader md, TypeReferenceHandle handle)
    {
        TypeReference reference = md.GetTypeReference(handle);
        return reference.ResolutionScope.Kind switch
        {
            HandleKind.AssemblyReference => md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)reference.ResolutionScope).Name),
            HandleKind.TypeReference => Scope(md, (TypeReferenceHandle)reference.ResolutionScope),
            _ => null,
        };
    }

    internal static string FullName(MetadataReader md, TypeReferenceHandle handle)
    {
        TypeReference reference = md.GetTypeReference(handle);
        string name = md.GetString(reference.Name);
        if (reference.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            return FullName(md, (TypeReferenceHandle)reference.ResolutionScope) + "+" + name;
        }

        string ns = md.GetString(reference.Namespace);
        return ns.Length > 0 ? ns + "." + name : name;
    }

    private static string TypeName(MetadataReader md, EntityHandle handle, NameProvider provider) => handle.Kind switch
    {
        HandleKind.TypeReference => FullName(md, (TypeReferenceHandle)handle),
        HandleKind.TypeDefinition => md.GetString(md.GetTypeDefinition((TypeDefinitionHandle)handle).Namespace) + "."
            + md.GetString(md.GetTypeDefinition((TypeDefinitionHandle)handle).Name),
        HandleKind.TypeSpecification => provider.SpecScope((TypeSpecificationHandle)handle).Type,
        _ => "?",
    };

    private static string TypeOf(MetadataReader md, EntityHandle constructor)
    {
        EntityHandle parent = constructor.Kind switch
        {
            HandleKind.MemberReference => md.GetMemberReference((MemberReferenceHandle)constructor).Parent,
            HandleKind.MethodDefinition => md.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType(),
            _ => default,
        };
        return parent.Kind == HandleKind.TypeReference ? FullName(md, (TypeReferenceHandle)parent) : "";
    }
}

/// <summary>Names a type specification by its generic type (List`1 of a LiveSplit type is List`1's scope).</summary>
internal sealed class NameProvider(MetadataReader md) : ISignatureTypeProvider<(string? Scope, string Type), object?>
{
    public (string? Scope, string Type) SpecScope(TypeSpecificationHandle handle) =>
        md.GetTypeSpecification(handle).DecodeSignature(this, null);

    public (string?, string) GetPrimitiveType(PrimitiveTypeCode typeCode) => (null, typeCode.ToString());
    public (string?, string) GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) =>
        (null, reader.GetString(reader.GetTypeDefinition(handle).Name));
    public (string?, string) GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) =>
        (Scan.Scope(reader, handle), Scan.FullName(reader, handle));
    public (string?, string) GetGenericInstantiation((string?, string) genericType, ImmutableArray<(string?, string)> typeArguments) =>
        (genericType.Item1, genericType.Item2 + "<" + string.Join(",", typeArguments.Select(a => a.Item2)) + ">");
    public (string?, string) GetArrayType((string?, string) elementType, ArrayShape shape) => (elementType.Item1, elementType.Item2 + "[,]");
    public (string?, string) GetSZArrayType((string?, string) elementType) => (elementType.Item1, elementType.Item2 + "[]");
    public (string?, string) GetByReferenceType((string?, string) elementType) => elementType;
    public (string?, string) GetPointerType((string?, string) elementType) => elementType;
    public (string?, string) GetPinnedType((string?, string) elementType) => elementType;
    public (string?, string) GetModifiedType((string?, string) modifier, (string?, string) unmodifiedType, bool isRequired) => unmodifiedType;
    public (string?, string) GetFunctionPointerType(MethodSignature<(string?, string)> signature) => (null, "fnptr");
    public (string?, string) GetGenericMethodParameter(object? genericContext, int index) => (null, "!!" + index);
    public (string?, string) GetGenericTypeParameter(object? genericContext, int index) => (null, "!" + index);
    public (string?, string) GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
        SpecScope(handle);
}

/// <summary>Answers the look-alikes (and the app beside them) before the framework.</summary>
internal sealed class CheckContext(string compat) : System.Runtime.Loader.AssemblyLoadContext("check")
{
    protected override System.Reflection.Assembly? Load(System.Reflection.AssemblyName name)
    {
        foreach (string dir in new[] { compat, Path.GetDirectoryName(Path.GetFullPath(compat))! })
        {
            string file = Path.Combine(dir, name.Name + ".dll");
            if (File.Exists(file) && name.Name is not ("System.Runtime" or "netstandard" or "mscorlib"))
            {
                return LoadFromAssemblyPath(Path.GetFullPath(file));
            }
        }

        return null;
    }
}
