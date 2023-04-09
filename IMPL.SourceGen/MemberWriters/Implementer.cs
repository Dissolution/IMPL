﻿using System.Diagnostics;

namespace IMPL.SourceGen.MemberWriters;

public class Implementer
{
    public ImplSpec ImplSpec { get; }

    public IFieldSigWriter FieldWriter { get; internal set; }
    public IPropertySigWriter PropertyWriter { get; internal set; }
    public IEventSigWriter EventWriter { get; internal set; }

    public List<(MemberPos, object)> Members { get; }

    public List<IImplModifier> ImplModifiers { get; }

    public Implementer(ImplSpec spec)
    {
        ImplSpec = spec;
        // Use default fieldwriter
        FieldWriter = DefaultFieldSigWriter.Instance;
        // Assume simple property writer unless we override
        PropertyWriter = SimplePropertySigWriter.Instance;
        // Same for events
        EventWriter = SimpleEventSigWriter.Instance;
        // Setup ctor + methodwriters
        Members = new()
        {
            new(PropertyConstructorWriter.MemberPos, new PropertyConstructorWriter()),
        };

        ImplModifiers = new()
        {
            new BackingFieldModifier(),
        };


        // Add all known members
        foreach (var member in spec.Members)
        {
            AddMember(member);
        }

        // Pre-register non-interface specific
        this.ImplModifiers.Where(im => im is not IInterfaceImplModifer).Consume(im => im.PreRegister(this));

        // Scan interfaces for writer overrides
        foreach (var interfaceType in spec.InterfaceTypes)
        {
            var handler = this.ImplModifiers.FirstOrDefault(io => io is IInterfaceImplModifer iim && iim.AppliesTo(interfaceType));
            if (handler is not null)
            {
                handler.PreRegister(this);
            }
        }
    }


    public void AddMember(MemberPos memberPos, ICodeWriter codeWriter)
    {
        Members.Add((memberPos, codeWriter));
    }

    public void AddMember(MemberSig memberSig)
    {
        MemberPos memberPos = new()
        {
            Instic = memberSig.Instic,
            Visibility = memberSig.Visibility,
            MemberType = memberSig.MemberType,
        };
        Members.Add((memberPos, memberSig));
    }

    public IEnumerable<TMember> GetMembers<TMember>()
        where TMember : MemberSig
    {
        return this.Members
            .Select(static p => p.Item2)
            .OfType<TMember>();
    }



    private void WriteInterfaces(CodeBuilder codeBuilder)
    {
        var spec = ImplSpec;
        var interfaceTypes = spec.InterfaceTypes;

        if (interfaceTypes.Count == 0) return;
        codeBuilder.AppendLine(" : ")
            .IndentBlock(ib => ib.DelimitAppend(static b => b.Append(',').NewLine(), interfaceTypes));
    }


    public SourceCode Implement()
    {
        // Here we go
        var spec = ImplSpec;
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
            .AppendValue(implType.ObjType, "lc").Append(' ')
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
                            var sectionMembers = Members.Where(p =>
                            {
                                var pos = p.Item1;
                                return pos.Instic == instic && pos.MemberType == memberType && pos.Visibility == visibility;
                            }).ToList();

                            if (sectionMembers.Count == 0) continue;

                            foreach ((MemberPos pos, object? obj) in sectionMembers)
                            {
                                if (obj is ICodeWriter codeWriter)
                                {
                                    codeWriter.Write(this, codeBuilder);
                                }
                                else if (obj is FieldSig fieldSig)
                                {
                                    FieldWriter.Write(fieldSig, codeBuilder);
                                }
                                else if (obj is PropertySig propertySig)
                                {
                                    PropertyWriter.Write(propertySig, codeBuilder);
                                }
                                else if (obj is EventSig eventSig)
                                {
                                    EventWriter.Write(eventSig, codeBuilder);
                                }
                                else if (obj is MethodSig methodSig)
                                {
                                    // Ignore Getters + Setters, they'll have been handled by their property
                                    if (methodSig.Name.StartsWith("get_") ||
                                        methodSig.Name.StartsWith("set_"))
                                    {
                                        // Ignore
                                        continue;
                                    }

                                    throw new NotImplementedException();
                                }
                                else
                                    Debugger.Break();
                            }

                            // Newline before next section!
                            typeBlock.NewLine();
                        }
                    }
                }


                // Cleanup excess whitespace
                typeBlock.TrimEnd();
            });

        string fileName = $"{implType.FullName}.g.cs";
        string code = codeBuilder.ToString();
        return new SourceCode(fileName, code);
    }
}