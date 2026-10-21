namespace JastipApp.Models;

/// <summary>Peran pengguna. Menentukan menu dan aksi yang boleh dilakukan.</summary>
public enum Peran
{
    Admin,      // mengawasi semua event dan pesanan, boleh mengubah/menghapus apa pun
    Penyedia,   // membuka event jastip miliknya sendiri dan melihat pesanan yang masuk
    Pemesan     // menitip barang (membuat pesanan) dan melihat pesanannya sendiri
}

/// <summary>Akun pengguna aplikasi. Password tidak pernah disimpan sebagai teks biasa.</summary>
public class Pengguna
{
    public int Id { get; set; }
    public string Username { get; set; } = "";      // selalu huruf kecil, unik
    public string Nama { get; set; } = "";
    public string PasswordHash { get; set; } = "";  // format: pbkdf2$iterasi$salt$hash
    public Peran Peran { get; set; }
    public string Kontak { get; set; } = "";
    public string Alamat { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
