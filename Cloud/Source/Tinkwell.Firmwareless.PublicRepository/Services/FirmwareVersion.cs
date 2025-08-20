namespace Tinkwell.Firmwareless.PublicRepository.Services;

sealed class FirmwareVersion : IComparable<FirmwareVersion>, IEquatable<FirmwareVersion>
{
    public sealed class VersionStringComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x is null && y is null)
                return 0;

            if (x is null)
                return -1;

            if (y is null)
                return 1;

            return Parse(x).CompareTo(Parse(y));
        }
    }

    public ushort Major { get; }
    public ushort Minor { get; }
    public uint Revision { get; }
    public uint Build { get; }

    public string? Suffix { get; }

    private FirmwareVersion(ushort major, ushort minor, uint revision, uint build, string? suffix)
    {
        Major = major;
        Minor = minor;
        Revision = revision;
        Build = build;
        Suffix = suffix;
    }

    public static bool Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Contains('/') || input.Contains('\\')) 
            return false;

        var core = input.Split('-', 2)[0];
        var parts = core.Split('.', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length is < 1 or > 4)
            return false;

        if (!ushort.TryParse(parts[0], out _)) return false; // Major
        if (parts.Length > 1 && !ushort.TryParse(parts[1], out _)) return false; // Minor
        if (parts.Length > 2 && !uint.TryParse(parts[2], out _)) return false; // Revision
        if (parts.Length > 3 && !uint.TryParse(parts[3], out _)) return false; // Build

        return true;
    }

    public static FirmwareVersion Parse(string input)
    {
        if (!Validate(input))
            throw new FormatException($"Invalid firmware version format: '{input}'");

        var split = input.Split('-', 2);
        var core = split[0];
        var suffix = split.Length > 1 ? split[1] : null;

        var parts = core.Split('.', StringSplitOptions.RemoveEmptyEntries);

        ushort major = ushort.Parse(parts[0]);
        ushort minor = parts.Length > 1 ? ushort.Parse(parts[1]) : (ushort)0;
        uint revision = parts.Length > 2 ? uint.Parse(parts[2]) : 0;
        uint build = parts.Length > 3 ? uint.Parse(parts[3]) : 0;

        return new FirmwareVersion(major, minor, revision, build, suffix);
    }

    public int CompareTo(FirmwareVersion? other)
    {
        if (other is null)
            return 1;

        int result = Major.CompareTo(other.Major);
        if (result != 0)
            return result;

        result = Minor.CompareTo(other.Minor);
        if (result != 0)
            return result;

        result = Revision.CompareTo(other.Revision);
        if (result != 0)
            return result;

        return Build.CompareTo(other.Build);
    }

    public bool Equals(FirmwareVersion? other)
    {
        if (other is null)
            return false;

        return Major == other.Major &&
               Minor == other.Minor &&
               Revision == other.Revision &&
               Build == other.Build;
    }

    public override bool Equals(object? obj)
        => obj is FirmwareVersion fv && Equals(fv);

    public override int GetHashCode()
        => HashCode.Combine(Major, Minor, Revision, Build);

    public override string ToString()
    {
        if (Major == 0 && Minor == 0 && Revision == 0 && Build == 0)
            return "0.0";

        var version = $"{Major}.{Minor}.{Revision}.{Build}".TrimEnd(".0".ToCharArray());
        return Suffix is null ? version : $"{version}-{Suffix}";
    }

    public static bool operator ==(FirmwareVersion a, FirmwareVersion b) 
        => a?.Equals(b) ?? b is null;

    public static bool operator !=(FirmwareVersion a, FirmwareVersion b) 
        => !(a == b);

    public static bool operator <(FirmwareVersion a, FirmwareVersion b) 
        => a.CompareTo(b) < 0;

    public static bool operator <=(FirmwareVersion a, FirmwareVersion b) 
        => a.CompareTo(b) <= 0;

    public static bool operator >(FirmwareVersion a, FirmwareVersion b) 
        => a.CompareTo(b) > 0;

    public static bool operator >=(FirmwareVersion a, FirmwareVersion b) 
        => a.CompareTo(b) >= 0;
}