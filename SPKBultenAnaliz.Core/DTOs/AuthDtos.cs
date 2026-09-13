using System;

namespace SPKBultenAnaliz.Core.DTOs
{
    public class GirisRequest
    {
        public string KullaniciAdi { get; set; } = string.Empty;
        public string Sifre { get; set; } = string.Empty;
    }

    public class GirisResponse
    {
        public string Token { get; set; } = string.Empty;
        public string KullaniciAdi { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public DateTime SonaErmeTarihi { get; set; }
    }

    public class KullaniciDto
    {
        public int Id { get; set; }
        public string KullaniciAdi { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public DateTime OlusturmaTarihi { get; set; }
        public DateTime? SonGirisTarihi { get; set; }
        public bool Aktif { get; set; }
    }

    public class KullaniciEkleRequest
    {
        public string KullaniciAdi { get; set; } = string.Empty;
        public string Sifre { get; set; } = string.Empty;
        public string Rol { get; set; } = "Admin";
    }

    public class SifreDegistirRequest
    {
        public string EskiSifre { get; set; } = string.Empty;
        public string YeniSifre { get; set; } = string.Empty;
    }
}
