﻿using System.Diagnostics;

namespace IMPL.SourceGen.Scratch2;

public readonly record struct MemberPos(Instic Instic, MemberTypes MemberType, Visibility Visibility);


public class Implementer
{
    public ImplSpec ImplSpec { get; }

    public IFieldSigWriter FieldWriter { get; }
    public IPropertySigWriter PropertyWriter { get; }
    public IEventSigWriter EventWriter { get; }

    public List<(MemberPos, object)> Members { get; }


    public List<InterfaceOverrides> InterfaceOverrides { get; }

    public Implementer(ImplSpec spec)
    {
        this.ImplSpec = spec;
        // Use default fieldwriter
        this.FieldWriter = DefaultFieldSigWriter.Instance;
        // Assume simple property writer unless we override
        this.PropertyWriter = SimplePropertySigWriter.Instance;
        // Same for events
        this.EventWriter = SimpleEventSigWriter.Instance;
        // Setup ctor + methodwriters
        this.Members = new();

        this.InterfaceOverrides = new();


        // Add all known members
        foreach (var member in spec.Members)
        {
            AddMember(member);
        }

        // Scan interfaces for writer overrides
        foreach (var interfaceType in spec.InterfaceTypes)
        {
            var handler = this.InterfaceOverrides.FirstOrDefault(io => io.AppliesTo(interfaceType));
            if (handler is not null)
            {
                handler.PreRegister(this);
            }
        }
    }


    public void AddMember(MemberPos memberPos, ICodeWriter codeWriter)
    {
        this.Members.Add((memberPos, codeWriter));
    }

    public void AddMember(MemberSig memberSig)
    {
        MemberPos memberPos = new()
        {
            Instic = memberSig.Instic,
            Visibility = memberSig.Visibility,
            MemberType = memberSig.MemberType,
        };
        this.Members.Add((memberPos, memberSig));
    }

    private void WriteInterfaces(CodeBuilder codeBuilder)
    {
        var spec = this.ImplSpec;
        var interfaceTypes = spec.InterfaceTypes;

        if (interfaceTypes.Count == 0) return;
        codeBuilder.AppendLine(" : ")
            .DelimitAppend(static b => b.Append(',').NewLine(), interfaceTypes);
    }


    public SourceCode Implement()
    {
        // Here we go
        var spec = this.ImplSpec;
        var implType = spec.ImplType;


        using var codeBuilder = new CodeBuilder();
        codeBuilder
            .AutoGeneratedHeader()
            .Nullable(true)
            // usings
            .Namespace(implType.Namespace)
            .NewLine()
            // type decleration
            .AppendValue(implType.Visibility, "lc")
            .AppendIf(implType.Instic == Instic.Static, " static ", " ")
            .AppendKeywords(implType.Keywords)
            .AppendValue(implType.ObjType, "lc")
            .Append(implType.Name)
            .If(spec.InterfaceTypes.Count > 0, WriteInterfaces)
            .BracketBlock(typeBlock =>
            {
                // Static, Instance
                foreach (Instic instic in new[] { Instic.Static, Instic.Instance })
                {
                    // operators, fields, properties, events, constructors, methods
                    foreach (MemberTypes memberType in new[] { MemberTypes.Custom, MemberTypes.Field, MemberTypes.Property, MemberTypes.Event, MemberTypes.Constructor, MemberTypes.Method })
                    {
                        // private -> public
                        foreach (Visibility visibility in new[] { Visibility.Private, Visibility.Protected, Visibility.Protected | Visibility.Internal, Visibility.Internal, Visibility.Public })
                        {
                            // What do we have in this section?
                            var sectionMembers = this.Members.Where(p =>
                            {
                                var pos = p.Item1;
                                return pos.Instic == instic && pos.MemberType == memberType && pos.Visibility == visibility;
                            }).ToList();

                            if (sectionMembers.Count > 0)
                                Debugger.Break();                        
                        }
                    }
                }
            });

        string fileName = $"{implType.FullName}.g.cs";
        string code = codeBuilder.ToString();
        return new SourceCode(fileName, code);
    }
}

public abstract class InterfaceOverrides
{
    public abstract bool AppliesTo(TypeSig interfaceType);

    public abstract void PreRegister(Implementer implementer);
}


public interface IFieldSigWriter
{
    void Write(FieldSig fieldSig, CodeBuilder codeBuilder);
}

public sealed class DefaultFieldSigWriter : IFieldSigWriter
{
    public static IFieldSigWriter Instance { get; } = new DefaultFieldSigWriter();

    public void Write(FieldSig fieldSig, CodeBuilder codeBuilder)
    {
        codeBuilder
            .AppendValue(fieldSig.Visibility, "lc")
            .AppendIf(fieldSig.Instic == Instic.Instance, " ", " static ")
            .AppendKeywords(fieldSig.Keywords)
            .Append(fieldSig.Name)
            .AppendLine(';');
    }
}

public sealed class SimplePropertySigWriter : IPropertySigWriter
{
    public static IPropertySigWriter Instance { get; } = new SimplePropertySigWriter();

    public void Write(PropertySig propertySig, CodeBuilder codeBuilder)
    {
        codeBuilder
            .AppendValue(propertySig.Visibility, "lc")
            .AppendIf(propertySig.Instic == Instic.Instance, " ", " static ")
            .AppendKeywords(propertySig.Keywords)
            .Append(propertySig.Name).Append(" {")
            .AppendIf(propertySig.GetMethod is not null, " get;")
            .If(propertySig.SetMethod is not null, setBlock =>
            {
                if (propertySig.SetMethod!.Keywords.HasFlag(Keywords.Init))
                    setBlock.Append(" init;");
                else
                    setBlock.Append(" set;");
            })
           .Append(" }").NewLine();
    }
}

public class PropertySetFieldWriter : IPropertySigWriter
{
    public void Write(PropertySig propertySig, CodeBuilder codeBuilder)
    {
        codeBuilder
            .AppendValue(propertySig.Visibility, "lc")
            .AppendIf(propertySig.Instic == Instic.Instance, " ", " static ")
            .AppendKeywords(propertySig.Keywords)
            .Append(propertySig.Name).Append(" {")
            .AppendIf(propertySig.GetMethod is not null, " get;")
            .If(propertySig.SetMethod is not null, setBlock =>
            {
                if (propertySig.SetMethod!.Keywords.HasFlag(Keywords.Init))
                    setBlock.Append(" init;");
                else
                    setBlock.Append(" set;");
            })
           .Append(" }").NewLine();
    }
}

public sealed class SimpleEventSigWriter : IEventSigWriter
{
    public static IEventSigWriter Instance { get; } = new SimpleEventSigWriter();

    public void Write(EventSig eventSig, CodeBuilder codeBuilder)
    {
        codeBuilder
            .AppendValue(eventSig.Visibility, "lc")
            .AppendIf(eventSig.Instic == Instic.Instance, " ", " static ")
            .AppendKeywords(eventSig.Keywords)
            .Append(" event ")
            .AppendValue(eventSig.EventType)
            .Append(' ').Append(eventSig.Name).AppendLine(';');
    }
}

public interface IPropertySigWriter
{
    void Write(PropertySig propertySig, CodeBuilder codeBuilder);
}

public interface IPropertyImplementer
{
    IPropertySigWriter GetPropertyWriter();


}

public interface IEventSigWriter
{
    void Write(EventSig eventSig, CodeBuilder codeBuilder);
}

public interface ICodeWriter
{
    /// <summary>
    /// <see cref="CBA"/>
    /// </summary>
    /// <param name="codeBuilder"></param>
    void Write(CodeBuilder codeBuilder);
}

public interface IMemberSigCodeWriter : ICodeWriter
{
    void Write(MemberSig memberSig, CodeBuilder codeBuilder);
}