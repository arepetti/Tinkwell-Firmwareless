using System.Reflection;

namespace Tinkwell.Firmwareless.PublicRepository.Authentication;

static class Scopes
{
    public const string KeyCreate = "key.create";
    public const string KeyRead = "key.read";
    public const string KeyRevoke = "key.revoke";
    public const string KeyDelete = "key.delete";

    public const string VendorCreate = "vendor.create";
    public const string VendorRead = "vendor.read";
    public const string VendorUpdate = "vendor.update";
    public const string VendorDelete = "vendor.delete";

    public const string ProductCreate = "product.create";
    public const string ProductRead = "product.read";
    public const string ProductUpdate = "product.update";
    public const string ProductDelete = "product.delete";

    public const string FirmwareCreate = "firmware.create";
    public const string FirmwareRead = "firmware.read";
    public const string FirmwareUpdate = "firmware.update";
    public const string FirmwareDelete = "firmware.delete";

    public const string FirmwareDownloadAll = "firmware.download_all";

    public static string[] All()
    {
        return typeof(Scopes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(x => x.IsLiteral && !x.IsInitOnly && x.FieldType == typeof(string))
            .Select(x => (string)x.GetRawConstantValue()!)
            .ToArray();
    }

    public static string[] Parse(string? scopes)
    {
        if (string.IsNullOrWhiteSpace(scopes))
            return [];

        return scopes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }

    public static string ToString(string[] scopes)
        => string.Join(',', scopes.Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)));
}