﻿using KonataAdapter.Extensions;

namespace KonataAdapter.Message.Model;

public class AtChain : BaseChain
{
    public uint AtUin { get; }

    internal string? DisplayString { get; set; }

    private AtChain(uint uin)
        : base(ChainType.At, ChainMode.Multiple)
    {
        AtUin = uin;
    }

    public static AtChain Create(uint memberUin)
        => new(memberUin);

    internal static AtChain ParseKqCode(string code)
    {
        var args = GetKqCodeArgs(code);
        {
            var atUin = args["qq"];
            return Create(atUin == "all" ? 0 : uint.Parse(atUin));
        }
    }

    internal static AtChain ParseCsCode(string code) =>
        Create(Convert.ToUInt32(code.GetSandwichedText("[mirai:at", "]")));

    public override string ToKqCode()
        => $"[KQ:at,qq={(AtUin == 0 ? "all" : AtUin.ToString())}]";

    public override string ToCsCode()
        => $"[mirai:at:{(AtUin == 0 ? "all" : AtUin.ToString())}]";

    public override string ToPreviewString()
        => DisplayString ?? $"@{AtUin}";
}