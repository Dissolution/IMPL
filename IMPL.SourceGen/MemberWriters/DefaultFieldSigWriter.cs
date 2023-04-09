﻿namespace IMPL.SourceGen.MemberWriters;

public sealed class DefaultFieldSigWriter : IFieldSigWriter
{
    public static IFieldSigWriter Instance { get; } = new DefaultFieldSigWriter();

    public void Write(FieldSig fieldSig, CodeBuilder codeBuilder)
    {
        codeBuilder
            .AppendValue(fieldSig.Visibility, "lc")
            .AppendIf(fieldSig.Instic == Instic.Instance, " ", " static ")
            .AppendKeywords(fieldSig.Keywords)
            .Append(fieldSig.FieldType)
            .Append(' ')
            .Append(fieldSig.Name)
            .AppendLine(';');
    }
}
