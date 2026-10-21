namespace JastipApp.Services;

/// <summary>Kesalahan yang pesannya aman dan layak ditampilkan langsung ke pengguna.</summary>
public class AppException : Exception
{
    public AppException(string pesan) : base(pesan) { }
}

/// <summary>Aksi ditolak karena peran/kepemilikan tidak sesuai. Dicek di service, bukan hanya disembunyikan di UI.</summary>
public class AksesDitolakException : AppException
{
    public AksesDitolakException(string pesan = "Kamu tidak punya izin untuk melakukan aksi ini.") : base(pesan) { }
}
