using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

namespace GardenVR.Core.Tests;

// Unity-compiled app code (Terrarium and Sundial) compiles against the GardenVR.Core surface.
// Every type and member that is not private is listed in PublicSurface.approved.txt, one sorted line each. A change to that
// list is a change a Unity-compiled caller can see, so it is made by editing the approved file in the same commit.
// Setting GARDENVR_APPROVE_SURFACE=1 rewrites the approved file from the assembly instead of comparing.
public sealed class PublicSurfaceTests
{
    const string ApprovedName = "PublicSurface.approved.txt";
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    static string Here([CallerFilePath] string path = "") { return Path.GetDirectoryName(path); }

    [Fact]
    public void The_non_private_surface_matches_the_approved_file()
    {
        string approvedPath = Path.Combine(Here(), ApprovedName);
        string[] actual = Surface(typeof(GardenVR.Core.TerrariumSave).Assembly);

        if (Environment.GetEnvironmentVariable("GARDENVR_APPROVE_SURFACE") == "1")
            File.WriteAllText(approvedPath, string.Join("\n", actual) + "\n", new UTF8Encoding(false));

        string[] approved = File.ReadAllText(approvedPath).Split('\n')
            .Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray();

        string[] added = actual.Except(approved).ToArray();
        string[] removed = approved.Except(actual).ToArray();
        Assert.True(added.Length == 0 && removed.Length == 0,
            "public surface changed.\nadded:\n  " + string.Join("\n  ", added) + "\nremoved:\n  " + string.Join("\n  ", removed));
        Assert.Equal(approved.Length, actual.Length);
    }

    static string[] Surface(Assembly assembly)
    {
        var lines = new List<string>();
        foreach (Type type in assembly.GetTypes())
        {
            if (Generated(type) || !Reachable(type)) continue;
            AddType(type, lines);
        }
        lines.Sort(StringComparer.Ordinal);
        return lines.ToArray();
    }

    static bool Generated(MemberInfo m) { return m.IsDefined(typeof(CompilerGeneratedAttribute), false); }

    static bool Reachable(Type t)
    {
        for (Type cur = t; cur != null; cur = cur.DeclaringType)
            if (cur.IsNestedPrivate) return false;
        return true;
    }

    static void AddType(Type type, List<string> lines)
    {
        string kind = type.IsInterface ? "interface" : type.IsEnum ? "enum" : type.IsValueType ? "struct"
            : typeof(Delegate).IsAssignableFrom(type) ? "delegate" : "class";
        string mods = kind != "class" ? "" : type.IsAbstract && type.IsSealed ? "static " : type.IsAbstract ? "abstract " : type.IsSealed ? "sealed " : "";
        bool hasBase = type.BaseType != null && type.BaseType != typeof(object) && type.BaseType != typeof(ValueType) && type.BaseType != typeof(Enum);
        string ifaces = string.Join(",", type.GetInterfaces().Select(Name).OrderBy(s => s, StringComparer.Ordinal));
        string owner = Name(type);
        lines.Add("type " + TypeVis(type) + " " + mods + kind + " " + owner + (hasBase ? " : " + Name(type.BaseType) : "")
            + (ifaces.Length > 0 ? " implements " + ifaces : ""));

        foreach (FieldInfo f in type.GetFields(All))
        {
            if (Generated(f) || f.IsPrivate) continue;
            string lit = f.IsLiteral ? " = " + Convert.ToString(f.GetRawConstantValue(), CultureInfo.InvariantCulture) : "";
            lines.Add(owner + " field " + Vis(f.IsPublic, f.IsFamily, f.IsAssembly, f.IsFamilyOrAssembly, f.IsFamilyAndAssembly)
                + (f.IsLiteral ? " const" : f.IsStatic ? " static" : "") + (f.IsInitOnly ? " readonly" : "")
                + " " + Name(f.FieldType) + " " + f.Name + lit);
        }
        foreach (ConstructorInfo c in type.GetConstructors(All))
        {
            if (Generated(c) || c.IsPrivate) continue;
            lines.Add(owner + " ctor " + Vis(c) + (c.IsStatic ? " static" : "") + " (" + Params(c.GetParameters()) + ")");
        }
        foreach (PropertyInfo p in type.GetProperties(All))
        {
            if (Generated(p)) continue;
            string index = p.GetIndexParameters().Length > 0 ? "[" + Params(p.GetIndexParameters()) + "]" : "";
            Accessor(lines, owner, "property", "get", p.GetMethod, Name(p.PropertyType) + " " + p.Name + index);
            Accessor(lines, owner, "property", "set", p.SetMethod, Name(p.PropertyType) + " " + p.Name + index);
        }
        foreach (EventInfo e in type.GetEvents(All))
        {
            if (Generated(e)) continue;
            Accessor(lines, owner, "event", "add", e.AddMethod, Name(e.EventHandlerType) + " " + e.Name);
            Accessor(lines, owner, "event", "remove", e.RemoveMethod, Name(e.EventHandlerType) + " " + e.Name);
        }
        foreach (MethodInfo m in type.GetMethods(All))
        {
            if (Generated(m) || m.IsPrivate) continue;
            if (m.IsSpecialName && !m.Name.StartsWith("op_", StringComparison.Ordinal)) continue;
            string gen = m.IsGenericMethodDefinition ? "<" + string.Join(",", m.GetGenericArguments().Select(a => a.Name)) + ">" : "";
            string virt = m.IsAbstract ? " abstract" : m.IsVirtual && !m.IsFinal ? " virtual" : "";
            lines.Add(owner + " method " + Vis(m) + (m.IsStatic ? " static" : "") + virt + " " + Name(m.ReturnType) + " " + m.Name + gen
                + "(" + Params(m.GetParameters()) + ")");
        }
        foreach (Type nested in type.GetNestedTypes(All))
        {
            if (Generated(nested) || nested.IsNestedPrivate) continue;
            AddType(nested, lines);
        }
    }

    static void Accessor(List<string> lines, string owner, string what, string verb, MethodInfo m, string tail)
    {
        if (m == null || m.IsPrivate) return;
        lines.Add(owner + " " + what + " " + verb + " " + Vis(m) + (m.IsStatic ? " static" : "") + (m.IsAbstract ? " abstract" : m.IsVirtual && !m.IsFinal ? " virtual" : "") + " " + tail);
    }

    static string TypeVis(Type t)
    {
        if (t.IsNested) return Vis(t.IsNestedPublic, t.IsNestedFamily, t.IsNestedAssembly, t.IsNestedFamORAssem, t.IsNestedFamANDAssem);
        return t.IsPublic ? "public" : "internal";
    }

    static string Vis(MethodBase m) { return Vis(m.IsPublic, m.IsFamily, m.IsAssembly, m.IsFamilyOrAssembly, m.IsFamilyAndAssembly); }

    static string Vis(bool pub, bool fam, bool asm, bool famOrAsm, bool famAndAsm)
    {
        return pub ? "public" : famOrAsm ? "protected internal" : fam ? "protected" : asm ? "internal" : famAndAsm ? "private protected" : "private";
    }

    static string Params(ParameterInfo[] ps)
    {
        return string.Join(", ", ps.Select(p =>
        {
            string prefix = p.IsOut ? "out " : p.ParameterType.IsByRef ? "ref " : "";
            Type t = p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType;
            string def = p.HasDefaultValue ? " = " + (p.DefaultValue ?? "null") : "";
            return prefix + Name(t) + " " + p.Name + def;
        }));
    }

    static string Name(Type t)
    {
        if (t.IsArray) return Name(t.GetElementType()) + "[" + new string(',', t.GetArrayRank() - 1) + "]";
        if (t.IsGenericParameter) return t.Name;
        string name = t.IsNested ? Name(t.DeclaringType) + "." + t.Name : (t.Namespace == null ? "" : t.Namespace + ".") + t.Name;
        if (t.IsGenericType)
        {
            int tick = name.IndexOf('`');
            if (tick >= 0) name = name.Substring(0, tick);
            name += "<" + string.Join(",", t.GetGenericArguments().Select(Name)) + ">";
        }
        return name;
    }
}
