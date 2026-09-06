using Domina.Core.Dojo;
using Domina.Core.Dojo.Save;
using Godot;
using FileAccess = Godot.FileAccess;

namespace Domina.Game;

/// <summary>Kaydın diskteki tek yuvası.</summary>
/// <remarks>
/// <para>
/// Çekirdek kaydın <b>metnini</b> üretir ve okur (<see cref="DojoSaveFile"/>); bu sınıf
/// yalnızca o metni bir dosyaya koyar. Ayrım kasıtlı: dosya yolu ve motorun dosya
/// erişimi Godot'a bağlıdır, kaydın biçimi değildir.
/// </para>
/// <para>
/// Yazma <b>iki adımlı</b>: önce geçici dosya, sonra takas. Doğrudan üstüne yazılsaydı
/// yazma sırasında kapanan oyun, kaydı yarım bırakıp seferi silerdi — otomatik kayıt
/// her gün çalıştığı için bu pencere sık sık açılırdı.
/// </para>
/// </remarks>
public static class SaveSlot
{
    /// <summary>Kaydın yolu — kullanıcının veri klasöründe.</summary>
    public const string Path = "user://dojo.json";

    private const string TempPath = "user://dojo.json.new";

    /// <summary>Yüklenecek bir kayıt var mı?</summary>
    public static bool Exists() => FileAccess.FileExists(Path);

    /// <summary>Dojo'yu diske yazar; yazamazsa <c>false</c> döner.</summary>
    public static bool Write(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        string json = DojoSaveFile.Write(dojo);

        using (FileAccess? file = FileAccess.Open(TempPath, FileAccess.ModeFlags.Write))
        {
            if (file is null)
            {
                GD.PushWarning($"Kayıt yazılamadı: {FileAccess.GetOpenError()}");
                return false;
            }

            file.StoreString(json);
        }

        return Swap();
    }

    /// <summary>Kaydı okur. Dosya yoksa ya da okunamazsa başarısız bir sonuç döner.</summary>
    public static LoadResult Load()
    {
        if (!Exists())
        {
            return LoadResult.Failed("Kayıt bulunamadı.");
        }

        using FileAccess? file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
        return file is null
            ? LoadResult.Failed($"Kayıt açılamadı: {FileAccess.GetOpenError()}")
            : DojoSaveFile.Load(file.GetAsText());
    }

    /// <summary>Kaydı siler — yeni oyun eskisinin üstüne binmesin diye.</summary>
    public static void Delete()
    {
        using DirAccess? dir = DirAccess.Open("user://");
        if (dir is not null && dir.FileExists(Path))
        {
            dir.Remove(Path);
        }
    }

    /// <summary>Geçici dosyayı kaydın yerine koyar.</summary>
    private static bool Swap()
    {
        using DirAccess? dir = DirAccess.Open("user://");
        if (dir is null)
        {
            GD.PushWarning("Kayıt klasörü açılamadı.");
            return false;
        }

        if (dir.FileExists(Path))
        {
            dir.Remove(Path);
        }

        Error error = dir.Rename(TempPath, Path);
        if (error != Error.Ok)
        {
            GD.PushWarning($"Kayıt yerine konamadı: {error}");
            return false;
        }

        return true;
    }
}
